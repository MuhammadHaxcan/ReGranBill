# Manual Test Plan — Voucher Edit / Drilldown Consistency

Scope: the changes delivered in phases A, B, and C of the voucher-edit / drilldown audit.
Target environment: local dev (Postgres `regranbill` on localhost:5432, Angular dev server at https://localhost:50559, backend at `dotnet run`).

## Prerequisites

1. Backend build clean: `dotnet build` in `ReGranBill/ReGranBill.Server` (verified).
2. Frontend build clean: `npx ng build --configuration development` in `ReGranBill/regranbill.client` (verified).
3. Migrations applied: `__EFMigrationsHistory` contains `20260515000000_RepairOrphanedSourceEntryIds` (verified).
4. Orphan check: `SELECT COUNT(*) FROM inventory_lots l WHERE l."SourceEntryId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM journal_entries je WHERE je."Id" = l."SourceEntryId");` returns 0 (verified).

## Backend scenarios

### S1 — Purchase edit through generic editor is now rejected
1. Search a Purchase voucher in `/voucher-editor`.
   - Expected: `PurchaseVoucher` is no longer in the type dropdown (Phase C.4). If a caller hits the API directly with `voucherType=PurchaseVoucher`, expect HTTP 400 with body `"Purchase vouchers must be edited from the Purchase Voucher page so inventory lots stay consistent."` (Phase A.1).

### S2 — Purchase Return edit through generic editor is now rejected
1. Hit `PUT /api/voucher-editor` with `voucherType=PurchaseReturnVoucher`.
   - Expected: HTTP 400 with the equivalent rejection message (Phase A.1).

### S3 — Purchase update blocked when lots are consumed downstream
1. Create a purchase 100kg → wash 60kg of it.
2. Open the purchase in `/purchase-voucher/:id`.
   - Expected: yellow drilldown banner lists the washing voucher with router link (Phase C.3, B.5).
3. Reduce the line weight to 50kg, save.
   - Expected: HTTP 409 conflict, toast surfaces the existing `EnsurePurchaseStockChangesAllowed` message.
4. Increase the line weight to 130kg, save.
   - Expected: succeeds, lot's available balance grows by 30kg, washing voucher unaffected.

### S4 — Purchase delete on a consumed lot stays blocked
1. From S3, attempt delete via the dedicated `DELETE /api/purchase-vouchers/{id}`.
   - Expected: 409 conflict `Cannot delete a purchase voucher whose inventory lot has already been consumed.` Banner shows the washing voucher.

### S5 — Purchase line removal no longer fails with FK violation
1. Create a purchase with two lines. Neither lot is touched downstream.
2. Edit and submit with one of the two lines removed.
   - Expected: 200 OK. The removed line's lot is set to `Voided`, `SourceEntryId` becomes NULL, the link/transaction rows are gone. (Phase A.4 — sequencing fix in `PurchaseVoucherService.UpdateAsync`.)
3. Query `SELECT "Status", "SourceEntryId" FROM inventory_lots WHERE Id=<that lot>;` → `Voided`, NULL.

### S6 — Production/Washing chained byproduct blocks parent edit
1. Production P creates output lot A.
2. Washing W consumes A (writes `WashConsume` on A).
3. Edit P from `/production-voucher/:id`.
   - Expected: HTTP 409 `This production voucher cannot be changed because one or more output lots have already been consumed.`
4. Banner on P shows W as a consumer of A.
5. Now create scenario where W's output is a byproduct lot B with `ParentLotId = A` (this is a future scenario — currently outputs do not set ParentLotId for Production). Verify: editing P's voucher is still blocked. (Phase A.3 defense-in-depth; behavior unchanged for current schema, but the ChildLots query is exercised.)

### S7 — Purchase return defensive guard fires on voided source lot
1. Manually `UPDATE inventory_lots SET "Status" = 'Voided' WHERE Id = <some source lot of an existing PR>;` (simulating an inconsistent state).
2. Edit that PR from `/purchase-return/:id`.
   - Expected: 409 conflict `Cannot edit this purchase return because its source lot(s) are not open: <lot number>` (Phase A.2 — `EnsureExistingSourceLotsUsableAsync`).
3. Delete that PR.
   - Expected: 409 conflict with the same message (Phase A.2 — `SoftDeleteAsync` guard).
4. Reset lot status to Open: `UPDATE inventory_lots SET "Status" = 'Open' WHERE Id = <id>;` — operations work again.

### S8 — Downstream endpoints
1. `GET /api/purchase-vouchers/{id}/downstream` returns `[]` for unconsumed purchases, populated list for consumed ones with at least one washing/production consumer.
2. `GET /api/washing-vouchers/{id}/downstream` returns the consumers of the washing outputs.
3. `GET /api/production-vouchers/{id}/downstream` returns the consumers of the production outputs (including byproduct children).
4. `GET /api/purchase-returns/{id}/downstream` returns transactions on the same source lot that occurred AFTER the PR (e.g., subsequent washing).

## Frontend scenarios

### F1 — Banner renders only in edit mode and only with usage
1. Open `/purchase-voucher` (new). No banner.
2. Open `/purchase-voucher/<unconsumed id>`. No banner.
3. Open `/purchase-voucher/<consumed id>`. Yellow banner lists the consuming voucher with a router link.
4. Click the link in the banner → navigates to the consuming voucher's edit page.

### F2 — Banner appears on all four pages
- `/purchase-voucher/:id`, `/purchase-return/:id`, `/washing-voucher/:id`, `/production-voucher/:id` — repeat F1 step 3 on each.

### F3 — Generic editor no longer offers PV/PR
1. Open `/voucher-editor`. Type dropdown only contains Journal/Receipt/Payment/Sale/SaleReturn/Cartage. (Phase C.4.)
2. Inspect-and-poke: if a savvy user manually sends `PurchaseVoucher` to the API, server returns 400 (S1).

## Negative / regression checks

- Editing a Journal/Receipt/Payment/Sale/SaleReturn/Cartage voucher through `/voucher-editor` still works (Phase A.1 only blocked PV/PR).
- Creating a new purchase/PR/washing/production still works end-to-end.
- Existing Pending Purchases / Pending Productions screens still load and route to the edit pages with the banner appearing where applicable.

## Database verification queries

```sql
-- Should always be 0:
SELECT COUNT(*) FROM inventory_lots l
WHERE l."SourceEntryId" IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM journal_entries je WHERE je."Id" = l."SourceEntryId");

-- For a given purchase voucher id, list downstream consumers:
SELECT v."VoucherNumber", v."VoucherType", t."TransactionType", t."WeightKgDelta", l."LotNumber"
FROM inventory_voucher_links link
JOIN inventory_transactions src_tx ON src_tx."Id" = link."TransactionId"
JOIN inventory_lots l ON l."Id" = link."LotId"
JOIN inventory_transactions t ON t."LotId" = l."Id" AND t."TransactionType" <> 'PurchaseIn'
JOIN journal_vouchers v ON v."Id" = t."VoucherId"
WHERE link."VoucherId" = <purchase id> AND link."VoucherType" = 'PurchaseVoucher'
ORDER BY v."Date", t."Id";
```

## Not in scope (per prior decision)

- Sale / SaleReturn lot-level cascade (sales still don't write `InventoryTransaction`; remain editable through the generic editor).
- Audit log of prior values across edits.
- Test project / xUnit fixture creation (no test project exists in the solution).
