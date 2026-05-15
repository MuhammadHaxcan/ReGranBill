using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class DownstreamUsageService : IDownstreamUsageService
{
    private readonly AppDbContext _db;

    public DownstreamUsageService(AppDbContext db) => _db = db;

    public Task<List<DownstreamUsageDto>> GetForPurchaseAsync(int purchaseVoucherId) =>
        GetConsumersOfLotsCreatedByAsync(purchaseVoucherId, VoucherType.PurchaseVoucher, InventoryTransactionType.PurchaseIn);

    public Task<List<DownstreamUsageDto>> GetForWashingAsync(int washingVoucherId) =>
        GetConsumersOfLotsCreatedByAsync(washingVoucherId, VoucherType.WashingVoucher, InventoryTransactionType.WashOutput);

    public Task<List<DownstreamUsageDto>> GetForProductionAsync(int productionVoucherId) =>
        GetConsumersOfLotsCreatedByAsync(productionVoucherId, VoucherType.ProductionVoucher, InventoryTransactionType.ProductionOutput);

    public Task<bool> HasAnyForPurchaseAsync(int purchaseVoucherId) =>
        HasAnyConsumerAsync(purchaseVoucherId, VoucherType.PurchaseVoucher, InventoryTransactionType.PurchaseIn);

    public Task<bool> HasAnyForWashingAsync(int washingVoucherId) =>
        HasAnyConsumerAsync(washingVoucherId, VoucherType.WashingVoucher, InventoryTransactionType.WashOutput);

    public Task<bool> HasAnyForProductionAsync(int productionVoucherId) =>
        HasAnyConsumerAsync(productionVoucherId, VoucherType.ProductionVoucher, InventoryTransactionType.ProductionOutput);

    private async Task<bool> HasAnyConsumerAsync(
        int sourceVoucherId,
        VoucherType sourceVoucherType,
        InventoryTransactionType outputType)
    {
        var outputLotIds = await _db.InventoryVoucherLinks
            .AsNoTracking()
            .Where(x => x.VoucherId == sourceVoucherId && x.VoucherType == sourceVoucherType)
            .Join(_db.InventoryTransactions, link => link.TransactionId, tx => tx.Id, (link, tx) => new { link.LotId, tx.TransactionType })
            .Where(x => x.TransactionType == outputType)
            .Select(x => x.LotId)
            .Distinct()
            .ToListAsync();

        if (outputLotIds.Count == 0) return false;

        var directlyConsumed = await _db.InventoryTransactions
            .AsNoTracking()
            .AnyAsync(x => outputLotIds.Contains(x.LotId) && x.TransactionType != outputType);
        if (directlyConsumed) return true;

        // Phase A.3 defense-in-depth: child lots derived from this voucher's outputs.
        var childLotIds = await _db.InventoryLots
            .AsNoTracking()
            .Where(x => x.ParentLotId.HasValue && outputLotIds.Contains(x.ParentLotId!.Value)
                && x.Status != InventoryLotStatus.Voided)
            .Select(x => x.Id)
            .ToListAsync();
        if (childLotIds.Count == 0) return false;

        return await _db.InventoryTransactions.AsNoTracking()
            .AnyAsync(x => childLotIds.Contains(x.LotId));
    }

    public async Task<List<DownstreamUsageDto>> GetForPurchaseReturnAsync(int prVoucherId)
    {
        // A PurchaseReturn does not own lots — it consumes from the source purchase's lot.
        // "Downstream" for a PR means: other transactions that hit the same source lots AFTER this PR,
        // which the UI may want to surface so the user understands what shares the lot balance.
        var prFootprint = await _db.InventoryTransactions
            .AsNoTracking()
            .Where(x => x.VoucherId == prVoucherId
                && x.VoucherType == VoucherType.PurchaseReturnVoucher
                && x.TransactionType == InventoryTransactionType.PurchaseReturnOut)
            .Select(x => new { x.LotId, x.Id })
            .ToListAsync();

        if (prFootprint.Count == 0)
            return [];

        var lotIds = prFootprint.Select(x => x.LotId).Distinct().ToList();
        var earliestPrTxIdPerLot = prFootprint
            .GroupBy(x => x.LotId)
            .ToDictionary(g => g.Key, g => g.Min(t => t.Id));

        var laterTransactions = await _db.InventoryTransactions
            .AsNoTracking()
            .Where(x => lotIds.Contains(x.LotId)
                && !(x.VoucherId == prVoucherId && x.VoucherType == VoucherType.PurchaseReturnVoucher)
                && x.TransactionType != InventoryTransactionType.PurchaseIn)
            .ToListAsync();

        var relevant = laterTransactions
            .Where(x => x.Id > earliestPrTxIdPerLot.GetValueOrDefault(x.LotId, int.MaxValue))
            .ToList();

        return await EnrichAsync(relevant);
    }

    private async Task<List<DownstreamUsageDto>> GetConsumersOfLotsCreatedByAsync(
        int sourceVoucherId,
        VoucherType sourceVoucherType,
        InventoryTransactionType outputType)
    {
        var outputLotIds = await _db.InventoryVoucherLinks
            .AsNoTracking()
            .Where(x => x.VoucherId == sourceVoucherId && x.VoucherType == sourceVoucherType)
            .Join(_db.InventoryTransactions, link => link.TransactionId, tx => tx.Id, (link, tx) => new { link.LotId, tx.TransactionType })
            .Where(x => x.TransactionType == outputType)
            .Select(x => x.LotId)
            .Distinct()
            .ToListAsync();

        if (outputLotIds.Count == 0)
            return [];

        // Include byproduct/child lots so chained usage shows up too.
        var childLotIds = await _db.InventoryLots
            .AsNoTracking()
            .Where(x => x.ParentLotId.HasValue && outputLotIds.Contains(x.ParentLotId!.Value))
            .Select(x => x.Id)
            .ToListAsync();

        var allLotIds = outputLotIds.Concat(childLotIds).Distinct().ToList();

        var consumers = await _db.InventoryTransactions
            .AsNoTracking()
            .Where(x => allLotIds.Contains(x.LotId) && x.TransactionType != outputType)
            .ToListAsync();

        return await EnrichAsync(consumers);
    }

    private async Task<List<DownstreamUsageDto>> EnrichAsync(List<InventoryTransaction> transactions)
    {
        if (transactions.Count == 0)
            return [];

        var voucherIds = transactions.Select(x => x.VoucherId).Distinct().ToList();
        var lotIds = transactions.Select(x => x.LotId).Distinct().ToList();

        var voucherById = await _db.JournalVouchers
            .AsNoTracking()
            .Where(v => voucherIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var lotById = await _db.InventoryLots
            .AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id);

        return transactions
            .Where(t => voucherById.ContainsKey(t.VoucherId) && lotById.ContainsKey(t.LotId))
            .OrderBy(t => voucherById[t.VoucherId].Date)
            .ThenBy(t => t.Id)
            .Select(t =>
            {
                var v = voucherById[t.VoucherId];
                var lot = lotById[t.LotId];
                return new DownstreamUsageDto
                {
                    VoucherId = v.Id,
                    VoucherNumber = v.VoucherNumber,
                    VoucherType = v.VoucherType.ToString(),
                    Date = v.Date,
                    LotId = lot.Id,
                    LotNumber = lot.LotNumber,
                    TransactionType = t.TransactionType.ToString(),
                    QtyDelta = t.QtyDelta,
                    WeightKgDelta = t.WeightKgDelta
                };
            })
            .ToList();
    }
}
