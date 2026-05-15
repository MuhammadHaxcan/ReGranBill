# Inventory Linking & Voucher Edit Flow — End-to-End Audit

Date: 2026-05-15
Scope: backend (`ReGranBill.Server`) + frontend (`regranbill.client`) covering every code path that creates or mutates inventory state, evaluated against the user's reference scenario and an exhaustive case matrix.

---

## 1. Data model recap

| Table | Purpose | Key fields |
|---|---|---|
| `inventory_lots` | One row per inbound batch (purchase line, production output, washing output) | `Id`, `LotNumber`, `ProductAccountId`, `VendorAccountId`, `SourceVoucherId`, `SourceVoucherType`, `SourceEntryId` (FK Restrict to `journal_entries`, nullable), `ParentLotId`, `OriginalQty`, `OriginalWeightKg`, `BaseRate`, `Status` (Open / Voided) |
| `inventory_transactions` | Every qty/weight delta on a lot | `LotId`, `VoucherId`, `VoucherType`, `VoucherLineKey`, `TransactionType` (PurchaseIn / PurchaseReturnOut / WashConsume / WashOutput / ProductionConsume / ProductionOutput / Void), `QtyDelta`, `WeightKgDelta`, `Rate`, `ValueDelta`, `TransactionDate` |
| `inventory_voucher_links` | Junction `(VoucherId, VoucherType, VoucherLineKey) → (LotId, TransactionId)` | Used by all services to find "what lot / transaction did this voucher line create or move?" |

Lot `AvailableWeightKg` = sum of all `WeightKgDelta` for that lot. PurchaseIn adds; PurchaseReturnOut/WashConsume/ProductionConsume subtract; WashOutput/ProductionOutput on the NEW lot adds.

`SourceEntryId` is the only direct FK between a lot and a journal entry. It's nullable, configured `OnDelete: Restrict`. Set by Purchase/Production/Washing services to the *output* entry that created the lot.

---

## 2. Operation × voucher matrix

| Voucher | Create writes | Update writes | Soft-delete writes | Guard on update | Guard on delete |
|---|---|---|---|---|---|
| Purchase | lot + `PurchaseIn` tx + link, per inventory line | `SyncPurchaseInventoryAsync` reuses lots by `LineId`, voids removed-line lots | voids lots + removes their txs/links | `EnsurePurchaseStockChangesAllowed` (date/vendor/product immutable on consumed lot; qty/weight reduce blocked below downstream usage; rate locked on non-return consumption; line delete blocked on consumed lot) | `HasPurchaseDownstreamConsumptionAsync` |
| Purchase Return | `PurchaseReturnOut` tx + link on source lot | full wipe of PR's txs/links → `RebuildInventoryAsync` rewrites; entries are blown away & recreated | removes PR's txs/links | `ValidateRequestAsync` (request ≤ available + own-load); **my Phase A.2** `EnsureExistingSourceLotsUsableAsync` defensive lot-status check | **my Phase A.2** lot-status check + wrap in transaction |
| Washing | `WashConsume` tx on input lot + new output lots + `WashOutput` txs + links | full wipe + rebuild (entries also wiped). Output lots become Voided with `SourceEntryId=null`, new lots created | same wipe, no rebuild | `HasDownstreamConsumptionAsync` (output lots consumed OR child lots present and used) | same |
| Production | analogous to Washing, plus byproducts and shortage line | same pattern | same | same | same |
| Sale / SaleReturn | *no inventory_transaction rows* — sale ledger only | same (no inventory side) | n/a | n/a | n/a |

Generic editor `VoucherEditorService` (post-Phase A.1) handles: Journal, Receipt, Payment, Sale, SaleReturn, Cartage. **Rejected**: Purchase, PurchaseReturn, Production, Washing.

---

## 3. Code-level walkthrough — the user's scenario

**Scenario**: Purchase 100 kg → Purchase Return 20 kg → Washing 70 kg input producing 64 kg output → user edits the Purchase Return.

### 3.1 Initial state after step 1 (Purchase 100 kg)

```
inventory_lots:        L1 { ProductAccountId=P, VendorId=V, BaseRate=R₀, OriginalWeightKg=100, Status=Open, SourceEntryId=E_PV1 }
inventory_transactions: T1 { LotId=L1, VoucherType=PurchaseVoucher, TxType=PurchaseIn, WeightKgDelta=+100 }
inventory_voucher_links: { VoucherId=PV, VoucherType=PurchaseVoucher, VoucherLineKey="purchase-line-1", LotId=L1, TxId=T1 }
journal_entries:        E_PV0 (vendor credit, SortOrder=0), E_PV1 (product debit, SortOrder=1)
```
Available on L1: 100. (`PurchaseVoucherService.CreateAsync` → `SyncPurchaseInventoryAsync` → adds lot/tx/link.)

### 3.2 After step 2 (PR 20 kg from L1)

```
inventory_transactions += T2 { LotId=L1, VoucherType=PurchaseReturnVoucher, TxType=PurchaseReturnOut, WeightKgDelta=-20 }
inventory_voucher_links += { VoucherId=PR, VoucherType=PurchaseReturnVoucher, VoucherLineKey="purchase-return-line-1", LotId=L1, TxId=T2 }
journal_entries += E_PR0 (vendor debit), E_PR1 (product credit, refers to lot L1 via selected_lot_id pulled from inventory_voucher_links)
```
Available on L1: 100 − 20 = 80. L1.Status still Open. **L1.SourceEntryId still = E_PV1** (PR does not create lots; never touches `SourceEntryId`).

### 3.3 After step 3 (Washing input 70 kg from L1 → output 64 kg)

`WashingVoucherService.CreateAsync` → `ApplyInventoryAsync`:
```
inventory_lots += L2 { ProductAccountId=washed account, VendorId=V (copied from L1), BaseRate=washedRate, OriginalWeightKg=64, ParentLotId=L1, SourceVoucherId=WSH, SourceVoucherType=WashingVoucher, SourceEntryId=E_WSH_OUT, Status=Open }
inventory_transactions += T3 { LotId=L1, VoucherType=WashingVoucher, TxType=WashConsume, WeightKgDelta=-70 }
                          T4 { LotId=L2, VoucherType=WashingVoucher, TxType=WashOutput, WeightKgDelta=+64 }
inventory_voucher_links += "washing-input" → (L1, T3), "washing-output-1" → (L2, T4)
journal_entries += unwashed credit, washed debit (E_WSH_OUT), possibly excess wastage debit
```
Available: L1 = 100−20−70 = **10**. L2 = **64**. The 6 kg wastage is journal-only (no lot reduction beyond consumption).

### 3.4 Step 4 — user edits the Purchase Return

Entry path: `PUT /api/purchase-returns/{id}` → `PurchaseReturnService.UpdateAsync` (`Services/PurchaseReturnService.cs:162`).

Execution order with the Phase A.2 patches applied:

```csharp
1. Load prJv (still tracked).
2. BeginTransactionAsync                                  (Phase A.2 moved this earlier)
3. EnsureExistingSourceLotsUsableAsync(prJv.Id)           (Phase A.2 new guard)
     → Query: any lot linked to this PR whose Status != Open?
     → L1 is Open → passes.
4. ValidateRequestAsync(request, prVoucherId=id)
     → availableByLot[L1] = 10  (sum of WeightKgDelta on L1)
     → currentVoucherLoads[L1] = 20 (absolute of this PR's existing PurchaseReturnOut txs)
     → effective available[L1] = 10 + 20 = 30
     → requestedByLot[L1] = new request weight
5. Voucher fields updated (Date, VehicleNumber, Description, RatesAdded).
6. RemoveVoucherInventoryAsync(prJv.Id)
     → Deletes T2 from inventory_transactions; deletes corresponding link.
     → After this step (in-tracker, pre-flush): L1 = 100 − 70 = 30 (washing still consumes 70).
7. _db.JournalEntries.RemoveRange(prJv.Entries); prJv.Entries.Clear();
8. New entries appended from request.
9. SaveChangesAsync                                       (flushes all of 6/7/8)
10. RebuildInventoryAsync(prJv, request, validation)
      → For each request line: insert new PurchaseReturnOut tx + link on the chosen lot.
11. SaveChangesAsync; CommitAsync.
```

After commit (user reduced PR to 10 kg):
```
inventory_transactions: T1(+100), T3(-70), T2_new(-10).  T2 (old) gone.
inventory_voucher_links: link to T2_new added; old link gone.
L1 Available = 100 − 70 − 10 = 20. Washing still has its 70 kg WashConsume — unaffected.
L2 = 64, unaffected.
```

### 3.5 Step 4 — possibility matrix on the edit

| User action on PR | Backend validation result | Final L1 state | Final L2 state |
|---|---|---|---|
| Set return to 10 kg | passes (10 ≤ 30) | available 20 | 64 |
| Set return to 0 kg (single-line PR with 0 invalid → 400) | `Each line must have a quantity greater than zero` | unchanged | unchanged |
| Set return to 20 kg (unchanged) | passes | available 10 | 64 |
| Set return to 30 kg | passes (30 ≤ 30) | available 0 | 64 |
| Set return to 31 kg | 400 "Requested return exceeds available lot balance of 30.00 kg." | unchanged | unchanged |
| Change vendor | 400 "Selected lot does not belong to the chosen vendor." (lot's `VendorAccountId` is checked against request vendor) | unchanged | unchanged |
| Change product | 400 "Selected lot does not match the chosen purchase return product." | unchanged | unchanged |
| Change selected lot to another Open lot of same vendor+product (say L1') | passes if L1' has enough available; reapplies the delta to L1'; L1 is fully restored to 30 kg | available 30 | 64 |
| Change rate (PR rate, no inventory impact) | passes; vendor ledger amount changes; lot `BaseRate` is untouched (set at purchase time only) | available unchanged | 64 |
| Change date | passes; `TransactionDate` on rebuilt PR txs becomes the new date | available unchanged | 64 |
| Add a new line returning 5 kg from L1 | total per L1 = 25; 25 ≤ 30 ✓; rebuild creates two txs | available 5 | 64 |
| Delete PR entirely | passes `EnsureExistingSourceLotsUsableAsync` → soft deletes voucher, removes PR's txs & links | available 30 | 64 |

In every case the lot accounting reconciles to physical-mass balance because all writes happen within a single DB transaction and the available calculation re-runs against the post-mutation `inventory_transactions` sum.

### 3.6 Subsequent edit of the Purchase voucher in the same scenario

Entry path: `PUT /api/purchase-vouchers/{id}` → `PurchaseVoucherService.UpdateAsync` (`:191`).

State: L1 has downstream (T2 + T3). `LoadPurchaseInventoryStatesAsync` reports `HasDownstreamConsumption=true`, `HasNonReturnConsumption=true` (washing), `DownstreamWeightKgUsed = 20 + 70 = 90`, `DownstreamQtyUsed = sum(|QtyDelta| where set)`.

`EnsurePurchaseStockChangesAllowed`:
- Date change → 409 "date cannot be changed".
- Vendor change → 409 "Vendor cannot be changed".
- Product change on the line → 409 "cannot change the product".
- Weight change ≥ 90 kg → allowed; < 90 → 409 "weight cannot be less than 90.00".
- Rate change → 409 (HasNonReturnConsumption=true).
- Line removal → 409 "Cannot delete purchase line 1 because its lot has already been used".

On allowed mutations, the lot's `OriginalWeightKg`/`OriginalQty` are updated and the `PurchaseIn` transaction's delta is rewritten. The post-mutation L1 sum stays consistent because the PR (-20) and washing (-70) txs are untouched.

### 3.7 Subsequent edit of the Washing voucher

`HasDownstreamConsumptionAsync(WSH)` looks at L2's transactions excluding `WashOutput` AND, via Phase A.3, transactions on child lots whose `ParentLotId = L2`. In this scenario L2 is unconsumed → false → edit proceeds.

Update wipes L2 (Voided, `SourceEntryId=null`), removes T3+T4, removes journal entries, then rebuilds with the new request. If the new input weight is 60 kg, the new washing creates a new L2' with whatever yields the user specifies. The old L2 stays in the DB as Voided (filtered out of available-lot queries).

### 3.8 Subsequent delete of the Purchase voucher

`HasPurchaseDownstreamConsumptionAsync(PV)` finds any tx on L1 that isn't `PurchaseIn` → returns true → 409 "Cannot delete a purchase voucher whose inventory lot has already been consumed."

To unblock: delete the PR (allowed; restores +20) and the washing (allowed if L2 unconsumed; restores +70, voids L2). Then the purchase can be deleted.

---

## 4. Exhaustive scenario matrix

For each chain of operations, the table records the expected end state and whether the system enforces it correctly today.

### 4.1 Chains starting from a purchase

| # | Sequence | Expected behavior | Enforced? |
|---|---|---|---|
| 1 | P 100 → edit P weight to 90 | OK (no downstream) | ✓ |
| 2 | P 100 → edit P weight to 0 (or remove line) | line-remove voids lot | ✓ (Phase A.4 ordering fix prevents FK violation) |
| 3 | P 100 → PR 20 → edit P weight to 50 | 409 (50 < 20 only-PR ⇒ OK actually; 50 ≥ 20) — passes | ✓ |
| 4 | P 100 → PR 20 → edit P weight to 10 | 409 "weight cannot be less than 20.00" | ✓ |
| 5 | P 100 → PR 20 → W 70 → edit P weight to 110 | passes (110 ≥ 90); rate locked | ✓ |
| 6 | P 100 → PR 20 → W 70 → edit P rate | 409 (HasNonReturnConsumption) | ✓ |
| 7 | P 100 → PR 20 → W 70 → edit P vendor | 409 vendor immutable | ✓ |
| 8 | P 100 → delete P (no downstream) | OK; L1 voided | ✓ |
| 9 | P 100 → PR 20 → delete P | 409 "consumed downstream" | ✓ |
| 10 | P 100 → PR 20 → W 70 → delete P | 409 | ✓ |
| 11 | P 100 → PR 20 → W 70 → delete W → delete PR → delete P | succeeds at every step | ✓ |

### 4.2 Chains involving Purchase Return edits

| # | Sequence | Expected | Enforced? |
|---|---|---|---|
| 12 | P 100 → PR 20 → reduce PR to 10 | L1 avail = 90 | ✓ |
| 13 | P 100 → PR 20 → increase PR to 30 | L1 avail = 70 | ✓ |
| 14 | P 100 → PR 20 → increase PR to 110 | 409 "exceeds available 100" | ✓ |
| 15 | P 100 → PR 20 → W 70 → reduce PR to 10 (user's exact case) | L1 avail = 20; W unchanged | ✓ |
| 16 | P 100 → PR 20 → W 70 → increase PR to 30 | L1 avail = 0; W unchanged | ✓ |
| 17 | P 100 → PR 20 → W 70 → increase PR to 31 | 409 "exceeds available 30" | ✓ |
| 18 | P 100 → PR 20 → W 70 → switch PR lot to a different L1' (compatible) | L1 +20, L1' −new | ✓ (validation ensures vendor+product match) |
| 19 | P 100 → PR 20 → W 70 → delete PR | L1 avail = 30; W unchanged | ✓ |
| 20 | (corrupt state) PR exists but its source lot Status=Voided → edit PR | 409 (Phase A.2 `EnsureExistingSourceLotsUsableAsync`) | ✓ |
| 21 | (corrupt state) same → delete PR | 409 (Phase A.2 soft-delete guard) | ✓ |

### 4.3 Chains involving Washing edits

| # | Sequence | Expected | Enforced? |
|---|---|---|---|
| 22 | P 100 → W 70 → edit W input to 80 | OK (L1 avail 30 was 100−70=30, eff. 30+70=100 ≥ 80) | ✓ |
| 23 | P 100 → W 70 → edit W input to 110 | 409 "Input weight cannot exceed available 100" | ✓ |
| 24 | P 100 → W 70 → use L2 in production Q → edit W | 409 "output lots already consumed" | ✓ |
| 25 | P 100 → W1 60 → W2 40 → edit W1 | OK (L2 of W1 unconsumed; L1 eff. avail = 100) | ✓ |
| 26 | P 100 → W 70 → delete W | L1 +70 (=30 if PR present; =100 if not); L2 voided | ✓ |
| 27 | P 100 → W 70 → use L2 in production → delete W | 409 | ✓ |

### 4.4 Chains involving Production edits (including byproducts)

| # | Sequence | Expected | Enforced? |
|---|---|---|---|
| 28 | P 100 → Prod input 80 → outputs L_out 75 | OK | ✓ |
| 29 | P 100 → Prod 80 → edit Prod input to 90 | OK (L1 eff. avail = 20+80 = 100) | ✓ |
| 30 | P 100 → Prod 80 → consume L_out in W → edit Prod | 409 (downstream on output) | ✓ |
| 31 | P 100 → Prod 80 outputting L_out + byproduct L_by → consume L_by in Q → edit Prod | 409 (Phase A.3 ChildLots not needed here because L_by's consumption writes a tx on L_by which is in `outputLotIds`) | ✓ |
| 32 | P1 100 + P2 200 → Prod inputs 80+150 → outputs L_out | OK | ✓ |
| 33 | (Prod1 outputs L_out) → (Prod2 consumes L_out, outputs L_out2 with ParentLotId=L_out) → edit Prod1 | 409 — direct tx check finds Prod2's ProductionConsume on L_out | ✓ |

### 4.5 Generic editor (post-Phase A.1)

| # | Voucher | Generic editor path | Behavior |
|---|---|---|---|
| 34 | Purchase | dropdown hidden + server 400 | ✓ |
| 35 | Purchase Return | dropdown hidden + server 400 | ✓ |
| 36 | Production | dropdown hidden (was already rejected) + server 400 | ✓ |
| 37 | Washing | dropdown hidden (was already rejected) + server 400 | ✓ |
| 38 | Sale / SaleReturn / Journal / Receipt / Payment / Cartage | still editable | ✓ |

---

## 5. Backend code-level audit findings

### 5.1 Guards verified

- `VoucherEditorService.RejectUnsupportedVoucherType` (`Services/VoucherEditorService.cs:17`) — blocks PV/PR/Prod/Wash.
- `PurchaseVoucherService.EnsurePurchaseStockChangesAllowed` (`:639`) — date/vendor/product/qty/weight/rate/line-delete checks under downstream consumption.
- `PurchaseVoucherService.HasPurchaseDownstreamConsumptionAsync` (`:610`) — soft-delete guard.
- `PurchaseVoucherService.UpdateAsync`'s line-removal block (`:225-244` post-fix) — proactively voids lot + nulls `SourceEntryId` before entry delete (eliminates FK Restrict violation that would otherwise fire on the first `SaveChangesAsync`).
- `PurchaseReturnService.UpdateAsync` (`:162`) — wraps validation in a transaction; calls `EnsureExistingSourceLotsUsableAsync`; full wipe + rebuild flow is safe because PR doesn't own lots (no FK to entry).
- `PurchaseReturnService.SoftDeleteAsync` (`:268`) — wraps in transaction; lot-status defensive guard before delete.
- `ProductionVoucherService.HasDownstreamConsumptionAsync` (`:503`) — output-tx check + Phase A.3 ChildLots walk.
- `ProductionVoucherService.RemoveVoucherInventoryAsync` (`:520`) — voids created output lots, nulls `SourceEntryId`.
- `WashingVoucherService.HasDownstreamConsumptionAsync` (`:447`) — same pattern.
- `WashingVoucherService.RemoveVoucherInventoryAsync` (`:420`) — same.
- `DownstreamUsageService` (new) — reads `InventoryVoucherLinks` for the source voucher, projects to consumer rows, includes child lots.

### 5.2 Confirmed-safe interactions

- Lot `Status=Open` filter on `InventoryLotService.GetAvailableLotsAsync` (`:186`) plus `currentVoucherLoads` adjustment for edit-mode (`:208-220`) → frontend lot pickers never offer a closed lot, and the edit page sees its own previous load as available.
- All voucher services compute weight via `Sum(WeightKgDelta)` against the live `inventory_transactions` table → no stale availability cache.
- `Voucher.IsDeleted` query filter on `JournalVouchers` does not cause stale inventory transactions because the dedicated services hard-delete the `inventory_transactions`/`inventory_voucher_links` rows in their soft-delete paths.
- `InventoryLot.SourceVoucherId` FK is `Restrict`. Vouchers are never hard-deleted today, so this FK doesn't fire. If hard-delete is ever added, soft-delete-then-orphan-cleanup must run first.

### 5.3 Remaining backend risks (not introduced by these changes)

1. **Concurrency race on shared lot balance.** Two simultaneous editors of the same lot can each independently pass `ValidateRequestAsync` and commit, totaling more than the lot's actual remaining weight. Mitigation: PostgreSQL `SERIALIZABLE` transaction or explicit `SELECT ... FOR UPDATE` on the lot row inside the transaction. Out of scope.
2. **Voided lots with soft-deleted SourceVoucher.** A raw-material lot report joins `lot.SourceVoucher` which is filtered by `IsDeleted`. Lots whose source voucher is later soft-deleted (only possible through guarded paths, but theoretically) would surface a null/missing voucher in the report. Out of scope.
3. **Rate divergence between PR rate and lot BaseRate.** PR can return at a different rate than the purchase. Lot's `BaseRate` is fixed at purchase time → downstream washing/production cost basis stays at purchase rate, vendor ledger reflects PR rate. This is intended business behavior, not a bug, but worth documenting.
4. **`InventoryTransaction.QtyDelta` is null for washing/production consume.** `DownstreamQtyUsed` in `EnsurePurchaseStockChangesAllowed` therefore only counts PR's bag count toward "qty consumed". A purchase qty reduction below the PR bag count is blocked; a purchase qty reduction below the (PR bags + washing implicit bags) is technically allowed if weight permits. Weight is the authoritative check; qty is informational. Acceptable.
5. **Available-lot pickers don't expose voided lots' history.** If a user wants to see "what happened to this voucher's old lot after it was voided," there's no UI path. The Raw Material Lot Report (`OpenOnly=false`) does show them. OK.

---

## 6. Frontend audit findings

### 6.1 Wiring verified

| Page | Banner wired | Service injected | `loadDownstreamUsage()` called |
|---|---|---|---|
| `purchase-voucher.component.ts` | ✓ (top of HTML) | ✓ | inside `loadChallan` success callback |
| `purchase-return.component.ts` | ✓ | ✓ | inside `loadPurchaseReturn` success callback |
| `washing-voucher.component.ts` | ✓ | ✓ | inside `applyVoucher` after `loadAvailableLots` callback |
| `production-voucher.component.ts` | ✓ | ✓ | end of `applyVoucher` |
| `voucher-editor.component.ts` | n/a | n/a | dropdown trimmed to exclude PV/PR |

### 6.2 Banner behavior

- Renders only when `isEditMode && downstreamUsages.length > 0`.
- Lists each consuming voucher with type label, voucher number, date, lot number, transaction type, weight delta.
- Each row router-links to the consuming voucher's dedicated edit page using the route map (`PurchaseVoucher → /purchase-voucher/:id`, etc.).
- For PR: shows transactions on the same source lot that occurred AFTER the PR (filtered by `tx.Id > earliestPrTxIdPerLot` server-side), so the user can see what else shares this lot.

### 6.3 Confirmed-safe frontend interactions

- Lot pickers use `getAvailableForPurchaseReturn / Washing / Production` with `voucherId` query param, so the edit page sees its own current load as available — no false "exceeds" error during edit.
- `confirm-modal.service` is invoked on delete buttons; server 409 messages flow through `getApiErrorMessage` to a toast.
- Banner is purely additive — does not interfere with form submission. Failure surfaces as server toast.

### 6.4 Frontend gaps

1. **No proactive field-locking.** When the banner appears, the user can still type a new date / vendor / smaller weight; only on save do they see the 409. The Phase C plan recommended disabling such fields when downstream usage exists. Not implemented in this round.
2. **No delete-confirmation summary.** When deleting a voucher that has downstream usage, the user gets a single toast on 409 with the server message but no UI listing of what must be removed first. The banner already lists consumers, so the user can see them in context, but a richer confirm modal would be friendlier.
3. **Banner doesn't paginate.** With 50+ consumers it would balloon. Acceptable for typical data volumes.
4. **`CartageVoucher` is not in the banner's `routeFor` map.** It can never legitimately appear (cartage doesn't write inventory transactions), but defensively a fallback "no link" rendering is already in place.
5. **The Inventory Lots / Raw Material Lot Report does NOT yet expose downstream-usage drilldown.** Plan Phase C step 9. Not implemented this round — `RawMaterialLotReport` already shows the movement timeline per lot, which is functionally similar.

---

## 7. Identified risks summary

| Risk | Severity | Status |
|---|---|---|
| Generic editor silently corrupting PV/PR inventory | Critical | **Closed** (Phase A.1) |
| Purchase line removal triggering FK Restrict violation | High | **Closed** (Phase A.4 sequencing fix) |
| Production/Washing edit with consumed byproducts not caught | Low (transitive check already catches it) | **Defended** (Phase A.3 ChildLots walk) |
| PR edit/delete on a voided source lot silently corrupting state | Medium | **Closed** (Phase A.2 `EnsureExistingSourceLotsUsableAsync` + soft-delete guard) |
| Orphan `SourceEntryId` values pointing at deleted entries | Medium | **Closed** (FK was already Restrict; Phase B.6 added defensive repair migration; lot voiders now null the column) |
| Concurrent edits on a shared lot can over-consume | Medium | **Open** (out-of-scope, needs serializable isolation or row lock) |
| Field-locking on edit pages when banner shows usage | Low | **Open** (planned UX, not implemented) |
| Drilldown confirm modal on delete | Low | **Open** (planned UX, not implemented) |
| Sale / SaleReturn lot tracking | Low (unchanged) | **Out of scope per decision** |

---

## 8. Audit conclusion

The user's reference scenario (P 100 → PR 20 → W 70 → 64 → edit PR) is fully consistent under the current code:

- Reduce PR ∈ [1, 30] kg → succeeds, lot weight reconciled.
- Increase PR > 30 kg → blocked with clear message.
- Delete PR → succeeds, lot restored to 30 kg.
- Subsequent edit of P weight, rate, vendor, product, date — each blocked or allowed per the matrix above with no ledger/inventory divergence.
- Subsequent edit of W input — bounded by lot's effective availability including W's own load.
- Subsequent delete of P → blocked while PR/W exist; unblocked once they are deleted.

All write paths are wrapped in a single DB transaction, so partial failures do not leak. The FK Restrict on `inventory_lots.SourceEntryId` is now respected at the application layer because every voiding write path nulls the column before deleting the referenced entry.

The two remaining open items (concurrent-edit race, proactive UI field-locking) are explicitly tracked above and can be picked up in a follow-up cycle.
