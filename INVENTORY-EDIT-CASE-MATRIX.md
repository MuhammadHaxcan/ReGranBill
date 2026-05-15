# Voucher Edit/Delete — Downstream Case Matrix & Audit

Date: 2026-05-15. Companion to `INVENTORY-FLOW-AUDIT.md`. Covers every realistic combination of edit/delete after downstream movement, including the "return some available weight" case.

---

## 1. Code-level audit verification

### 1.1 Downstream-usage consolidation (✓ verified)

| Service | Method | Implementation | Source |
|---|---|---|---|
| `PurchaseVoucherService` | `HasPurchaseDownstreamConsumptionAsync` | one-line delegate | `:628 → _downstreamUsageService.HasAnyForPurchaseAsync` |
| `WashingVoucherService` | `HasDownstreamConsumptionAsync` | delegate | `:452 → _downstreamUsageService.HasAnyForWashingAsync` |
| `ProductionVoucherService` | `HasDownstreamConsumptionAsync` | delegate | `:507 → _downstreamUsageService.HasAnyForProductionAsync` |
| `DownstreamUsageService` | `HasAnyConsumerAsync` (private) | direct lot check + Phase A.3 ChildLots walk | single source of truth |

No hand-rolled inventory-transaction `AnyAsync` queries remain in the per-voucher services. Confirmed via `grep -rn "_db.InventoryTransactions.AnyAsync"` returning zero matches in voucher services.

Controllers all expose `/downstream` endpoint and inject `IDownstreamUsageService` (4 controllers verified). DI registration in `Program.cs:81`.

### 1.2 Category filtering moved server-side (✓ verified)

| Page | Server call | What it asks for |
|---|---|---|
| `customer-ledger` | `getFiltered([Party], [Customer, Vendor, Both])` | Categories that have at least one party with one of those roles |
| `washing-voucher` | `getFiltered([UnwashedMaterial])` + `getFiltered([RawMaterial])` | Input and output (washed) category dropdowns |
| `production-voucher` | `getFiltered([RawMaterial, Product])`, `getFiltered([Product])`, `getFiltered([RawMaterial, Product])`, `getFiltered([Expense])` | Input, output, byproduct, shortage category dropdowns |

No `allowedCategoryIds` or `buildCategoryOptionsForType` helpers remain in any page — `grep` confirmed.

Backend implementation: `CategoryService.GetFilteredAsync` (single LINQ query) → returns only categories that have at least one matching account. Endpoint: `GET /api/categories/filtered?accountTypes=...&partyRoles=...`. Parser tolerates whitespace, unknown tokens are silently dropped.

### 1.3 Production output type rule update (this turn)

| Position | Before | After |
|---|---|---|
| **Output line** | `AccountType.RawMaterial` only | `AccountType.Product` only |
| **Byproduct line** | `AccountType.RawMaterial` only | `AccountType.RawMaterial` **or** `AccountType.Product` |
| **Input line** | `RawMaterial` or `Product` | unchanged |
| **Shortage** | `Expense` | unchanged |

Backend: `ProductionVoucherService.cs:204-220`. Frontend: `production-voucher.component.html` output picker → `getFilteredAccountOptions(AccountType.Product, …)`, byproduct picker → new `getFilteredByproductAccountOptions(row.categoryId)` (which delegates to the RawMaterial+Product helper). Category dropdowns updated to match (output → `[Product]`, byproduct → `[RawMaterial, Product]`).

**Downstream implication of this rule change**: production now produces `Product` accounts. Those `Product` accounts can be consumed by another production (input accepts RawMaterial or Product) — so the existing `HasDownstreamConsumptionAsync` guard continues to function unchanged because it operates on the lot/transaction layer, not on account type.

---

## 2. The "return some available weight" scenarios

Base setup for these rows: **P 100kg purchase → lot L1 (BaseRate R₀, Status Open, AvailWt = 100)**.

Each row layers more events. "Available" = current lot weight balance.

| # | Sequence so far | L1 avail before user action | User action | Validation rule that fires | Result |
|---|---|---:|---|---|---|
| 1 | P 100 only | 100 | Return all 100kg in a new PR | `requested ≤ available` (100 ≤ 100) | ✓ PR created; L1=0kg, Open |
| 2 | P 100 only | 100 | Return 100.01kg | `requested > available` | ✗ 400 "Requested return exceeds available lot balance of 100.00 kg." |
| 3 | P 100 → PR 20 | 80 | Return another 80kg in new PR2 | PR2's own check: 80 ≤ 80 | ✓ PR2 created; L1=0kg |
| 4 | P 100 → PR 20 → W 70 | 10 | Try to return 10kg in a new PR2 | 10 ≤ 10 | ✓ PR2 created; L1=0kg |
| 5 | P 100 → PR 20 → W 70 | 10 | Try to return 11kg in a new PR2 | 11 > 10 | ✗ 400 "exceeds available 10.00 kg" |
| 6 | P 100 → PR 20 → W 70 | 10 | **Edit PR**, increase return from 20 → 25kg | `effective_avail = avail + own_load = 10 + 20 = 30; 25 ≤ 30` | ✓ Save; L1=5kg |
| 7 | P 100 → PR 20 → W 70 | 10 | Edit PR, increase return to 30kg | 30 ≤ 30 | ✓ Save; L1=0kg |
| 8 | P 100 → PR 20 → W 70 | 10 | Edit PR, increase return to 31kg | 31 > 30 | ✗ 400 "exceeds available 30.00 kg" |
| 9 | P 100 → PR 20 → W 70 | 10 | Edit PR, decrease return to 10kg | 10 ≤ 30 | ✓ Save; L1=20kg (10 added back) |
| 10 | P 100 → PR 20 → W 70 | 10 | Edit PR, decrease return to 0 (delete the only line) | line count must be ≥ 1 | ✗ 400 "Add at least one product line." |
| 11 | P 100 → PR 20 → W 70 | 10 | Delete PR entirely (soft-delete) | `EnsureExistingSourceLotsUsableAsync`: L1.Status=Open → passes | ✓ PR voided; L1=30kg |
| 12 | P 100 → W 80 (no PR yet) | 20 | Create new PR for 20kg | available 20, no own-load | ✓ PR1 created; L1=0kg |
| 13 | P 100 → W 80 → PR 20 → W 0 (wash deleted) | 80 | Edit PR, increase to 80kg | 80 ≤ avail(80)+own(20)=100; **but** request also ≤ original P weight=100. → 80 ≤ 100 | ✓ Save; L1=0kg |
| 14 | P 100 → PR 20 → W 70 → W2 5kg (washed remainder) | 5 | Edit PR, increase to 25kg | own_load=20; avail=5; effective=25; 25 ≤ 25 | ✓ Save; L1=0kg |
| 15 | P 100 → PR 20 → W 70 → W2 5kg | 5 | Edit PR, increase to 26kg | 26 > 25 | ✗ 400 "exceeds available 25.00 kg" |
| 16 | P 100 → PR 20 → W 70 → W2 5kg | 5 | Edit W (first washing), change input weight to 75kg | `HasDownstreamConsumptionAsync(W)`: L2 not consumed → false → proceeds; `available + own_load = 5 + 70 = 75; 75 ≤ 75` | ✓ Save (W now consumes 75); L1=0kg, L2_new replaces L2 |
| 17 | P 100 → PR 20 → W 70 → W2 5kg | 5 | Edit W (first), change input weight to 76kg | 76 > 75 | ✗ 400 "Input weight cannot exceed available lot balance of 75.00 kg." |
| 18 | P 100 → PR 20 → W 70 (output L2 used in Prod) | 10 | Edit W | `HasAnyForWashingAsync` finds Prod's `ProductionConsume` on L2 → true | ✗ 409 "This washing voucher cannot be changed because one or more output lots have already been consumed." |
| 19 | P 100 → PR 20 → W 70 (L2 used in Prod) | 10 | Delete W | same guard | ✗ 409 "Cannot delete a washing voucher after its output lots have been consumed." |
| 20 | P 100 → PR 20 → W 70 (L2 used in Prod) | 10 | Edit Prod | `HasAnyForProductionAsync`: Prod's output Lo not consumed → false; proceeds | ✓ Wipe + rebuild; L2 restored to 70, L_o becomes Voided, L_o_new created |
| 21 | P 100 → PR 20 → W 70 (L2 used in Prod) | 10 | Delete Prod | same | ✓ Soft-delete; L2 = 70 |
| 22 | P 100 → PR 20 → W 70 | 10 | Edit P, change qty/weight to ≥ 90kg | `EnsurePurchaseStockChangesAllowed`: `DownstreamWeightKgUsed = 20 + 70 = 90`; requested ≥ 90 | ✓ Save (if ≥ 90) |
| 23 | P 100 → PR 20 → W 70 | 10 | Edit P, change weight to 89.99kg | requested < 90 | ✗ 409 "weight cannot be less than 90.00 because that weight is already used downstream." |
| 24 | P 100 → PR 20 → W 70 | 10 | Edit P, change rate | `HasNonReturnConsumption=true` (washing) | ✗ 409 "rate cannot be changed... lot has already been consumed in washing or production." |
| 25 | P 100 → PR 20 → W 70 | 10 | Edit P, change vendor | any consumption | ✗ 409 "Vendor cannot be changed after one or more lots have been consumed." |
| 26 | P 100 → PR 20 → W 70 | 10 | Edit P, change date | any consumption | ✗ 409 "Purchase date cannot be changed after one or more lots have been consumed." |
| 27 | P 100 → PR 20 → W 70 | 10 | Edit P line product | line consumed | ✗ 409 "Cannot change the product on purchase line N because its lot has already been used." |
| 28 | P 100 → PR 20 → W 70 | 10 | Delete P | downstream non-`PurchaseIn` exists | ✗ 409 "Cannot delete a purchase voucher whose inventory lot has already been consumed." |
| 29 | P 100 only | 100 | Edit P, remove a line | Phase A.4 inline cleanup: lot → Voided, `SourceEntryId = null`, links/txs deleted **before** entry delete | ✓ Save; lot voided; no FK violation |
| 30 | P 100 → PR 20 → delete W → delete PR | 100 | Delete P | no downstream → false | ✓ Delete; lot voided |
| 31 | P (line1 100kg consumed, line2 50kg unused) | mixed | Edit P, remove line2 | line2 has no downstream | ✓ Save; line2's lot voided; line1 untouched |
| 32 | P (line1 100kg consumed, line2 50kg unused) | mixed | Edit P, remove line1 | line1 consumed | ✗ 409 "Cannot delete purchase line 1..." |
| 33 | P 100 → P2 (no downstream on either) | 100 | Edit P, change vendor | no consumption | ✓ Save |
| 34 | P 100 → PR 20 → fully delete PR → fully consume in W → fully consume W's output in Prod → delete Prod → delete W | 100 | Edit P | no downstream non-PurchaseIn tx → false | ✓ Save |
| 35 | Production with chained byproduct: P1 → Prod1 outputs Lo + Byproduct Lb (ParentLotId=null for both) → Lb consumed by Prod2 | … | Edit Prod1 | transitively: Prod2's `ProductionConsume` on Lb → caught by base check | ✗ 409 |
| 36 | Washing W → output L2 → Prod2 takes L2 as input → Prod2 produces Lp (ParentLotId=L2) → Lp consumed in Prod3 | … | Edit W | Phase A.3 ChildLots: walks `ParentLotId == L2`, finds Lp, checks transactions on Lp → Prod3's consume detected | ✗ 409 |
| 37 | (corrupt state) PR exists; source lot manually flipped to Voided | n/a | Edit PR | `EnsureExistingSourceLotsUsableAsync` fires | ✗ 409 "Cannot edit this purchase return because its source lot(s) are not open: …" |
| 38 | same corrupt state | n/a | Delete PR | same guard | ✗ 409 same message |
| 39 | **Generic editor**: try to edit PV or PR through `/api/voucher-editor` | n/a | n/a | `RejectUnsupportedVoucherType` | ✗ 400 "must be edited from the dedicated page..." |

---

## 3. The asymmetry to keep in mind

| | Purchase | Purchase Return | Production / Washing |
|---|---|---|---|
| Lock granularity | **per-line** (consumed lines locked, others free) | **per-lot validation** (request ≤ avail + own-load) | **per-voucher** (any consumed output → whole voucher locked) |
| Date editable when downstream exists | ✗ | ✓ | ✗ (whole edit blocked) |
| Vendor editable when downstream exists | ✗ | ✗ (lot-vendor must still match) | ✗ (whole edit blocked) |
| Product editable when downstream exists | ✗ on consumed lines | ✗ (lot-product must still match) | ✗ (whole edit blocked) |
| Reduce weight when downstream exists | OK ≥ `DownstreamWeightKgUsed` | OK down to 0 (delete adds back) | n/a — whole-voucher lock |
| Increase weight when downstream exists | OK | OK up to `avail + own_load` | n/a |
| Rate change when downstream exists | ✓ if only return consumption; ✗ if washing/production | always allowed (PR rate hits vendor ledger only, not lot BaseRate) | n/a |
| Delete when downstream exists | ✗ | ✓ (always safe — adds weight back) | ✗ |

---

## 4. Available-weight calculation reference

Where the magic numbers in the validation messages come from:

```
available[L]                 = Σ WeightKgDelta for all inventory_transactions on L
ownLoad_PR[L]                = Σ |WeightKgDelta| for PR's own PurchaseReturnOut txs on L (only on edit)
ownLoad_W[L]                 = Σ |WeightKgDelta| for W's own WashConsume txs on L (only on edit)
ownLoad_Prod[L]              = Σ |WeightKgDelta| for Prod's own ProductionConsume txs on L (only on edit)

PR edit check:               requested[L] ≤ available[L] + ownLoad_PR[L]
W edit check:                request.InputWeightKg ≤ available[L] + ownLoad_W[L]
Prod edit check (per input):  request[L] ≤ available[L] + ownLoad_Prod[L]
Purchase reduce check:       requested.Weight ≥ DownstreamWeightKgUsed
                             where DownstreamWeightKgUsed = Σ |WeightKgDelta| for non-PurchaseIn txs on L
```

All checks run within an explicit database transaction (`BeginTransactionAsync` → write → `SaveChanges` → `CommitAsync`).

---

## 5. The one open risk that's still real

Concurrent edits on the same lot can each independently pass their `available[L] + ownLoad` validation under PostgreSQL's default Read Committed isolation, then both commit. Net effect: lot could go negative by the difference. Fix would be `SELECT ... FOR UPDATE` on the lot row inside the transaction, or escalate to `SERIALIZABLE`. Out of scope; flagged.

The other ongoing gap is the sales side: sales don't write `inventory_transactions`, so `HasDownstreamConsumptionAsync` does not catch sale-side outflow. A production whose output is wholly sold (but not consumed by another washing/production) is still editable/deletable. Tracked separately; not addressed this turn.

---

## 6. Audit conclusion

- Downstream-usage consolidation: clean, single source of truth, Phase A.3 ChildLots walk preserved.
- Category filtering: now fully server-driven, no client-side derivation remains.
- Production output type rule: aligned (Output = Product; Byproduct = RawMaterial | Product) end-to-end.
- The "return available weight" semantics are correct for every scenario above, both for new PRs and for editing/deleting existing ones, including edge cases where the lot is over-allocated post-washing.
