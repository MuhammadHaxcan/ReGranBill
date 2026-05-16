using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Inventory;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class InventoryLotService : IInventoryLotService
{
    private readonly AppDbContext _db;

    public InventoryLotService(AppDbContext db) => _db = db;

    public Task<List<AvailableInventoryLotDto>> GetAvailableLotsForWashingAsync(int vendorId, int accountId, int? voucherId = null)
    {
        if (vendorId <= 0 || accountId <= 0)
            return Task.FromResult(new List<AvailableInventoryLotDto>());

        return GetAvailableLotsAsync(query =>
            query.Where(x => x.VendorAccountId == vendorId && x.ProductAccountId == accountId),
            voucherId,
            VoucherType.WashingVoucher,
            InventoryTransactionType.WashConsume);
    }

    public Task<List<AvailableInventoryLotDto>> GetAvailableLotsForProductionAsync(int accountId, int? voucherId = null)
    {
        if (accountId <= 0)
            return Task.FromResult(new List<AvailableInventoryLotDto>());

        return GetAvailableLotsAsync(query =>
            query.Where(x => x.ProductAccountId == accountId),
            voucherId,
            VoucherType.ProductionVoucher,
            InventoryTransactionType.ProductionConsume);
    }

    public Task<List<AvailableInventoryLotDto>> GetAvailableLotsForPurchaseReturnAsync(int vendorId, int accountId, int? voucherId = null)
    {
        if (vendorId <= 0 || accountId <= 0)
            return Task.FromResult(new List<AvailableInventoryLotDto>());

        return GetAvailableLotsAsync(query =>
            query.Where(x => x.VendorAccountId == vendorId && x.ProductAccountId == accountId),
            voucherId,
            VoucherType.PurchaseReturnVoucher,
            InventoryTransactionType.PurchaseReturnOut);
    }

    public async Task<Dictionary<int, decimal>> GetAvailableWeightByLotIdsAsync(IReadOnlyCollection<int> lotIds)
    {
        var ids = lotIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        return await _db.InventoryTransactions
            .AsNoTracking()
            .Where(x => ids.Contains(x.LotId))
            .GroupBy(x => x.LotId)
            .Select(g => new { g.Key, Available = VoucherHelpers.Round2(g.Sum(x => x.WeightKgDelta)) })
            .ToDictionaryAsync(x => x.Key, x => x.Available);
    }

    public async Task<RawMaterialLotReportDto> GetRawMaterialLotReportAsync(RawMaterialLotReportQueryDto query)
    {
        var lotsQuery = _db.InventoryLots
            .AsNoTracking()
            .Where(x => x.ProductAccount.AccountType == AccountType.RawMaterial || x.ProductAccount.AccountType == AccountType.UnwashedMaterial);

        if (query.VendorId.HasValue)
            lotsQuery = lotsQuery.Where(x => x.VendorAccountId == query.VendorId.Value);
        if (query.ProductId.HasValue)
            lotsQuery = lotsQuery.Where(x => x.ProductAccountId == query.ProductId.Value);
        if (!string.IsNullOrWhiteSpace(query.LotNumber))
        {
            var lotNumber = query.LotNumber.Trim();
            lotsQuery = lotsQuery.Where(x => x.LotNumber.Contains(lotNumber));
        }
        if (query.OpenOnly)
            lotsQuery = lotsQuery.Where(x => x.Status == InventoryLotStatus.Open);

        var lots = await lotsQuery
            .Select(x => new
            {
                Lot = x,
                ProductName = x.ProductAccount.Name,
                VendorName = x.VendorAccount == null ? null : x.VendorAccount.Name,
                SourceVoucherNumber = x.SourceVoucher.VoucherNumber,
                SourceDate = x.SourceVoucher.Date,
                AvailableWeightKg = x.Transactions.Sum(t => t.WeightKgDelta)
            })
            .OrderBy(x => x.Lot.CreatedAt)
            .ToListAsync();

        var lotIds = lots.Select(x => x.Lot.Id).ToArray();
        var movements = new List<RawMaterialLotMovementDto>();

        if (query.IncludeDetails && lotIds.Length > 0)
        {
            var movementRows = await _db.InventoryTransactions
                .AsNoTracking()
                .Where(x => lotIds.Contains(x.LotId))
                .Where(x => !query.From.HasValue || x.TransactionDate >= query.From.Value)
                .Where(x => !query.To.HasValue || x.TransactionDate <= query.To.Value)
                .Select(x => new
                {
                    x.Id,
                    x.LotId,
                    x.TransactionDate,
                    x.VoucherType,
                    x.TransactionType,
                    x.WeightKgDelta,
                    x.Rate,
                    x.ValueDelta,
                    x.Notes,
                    VoucherNumber = x.Voucher.VoucherNumber,
                    LotNumber = x.Lot.LotNumber
                })
                .OrderBy(x => x.LotId)
                .ThenBy(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .ToListAsync();

            var runningByLot = new Dictionary<int, decimal>();
            foreach (var row in movementRows)
            {
                var running = runningByLot.TryGetValue(row.LotId, out var current) ? current : 0m;
                running = VoucherHelpers.Round2(running + row.WeightKgDelta);
                runningByLot[row.LotId] = running;

                movements.Add(new RawMaterialLotMovementDto
                {
                    LotId = row.LotId,
                    LotNumber = row.LotNumber,
                    TransactionId = row.Id,
                    VoucherNumber = row.VoucherNumber,
                    VoucherType = row.VoucherType.ToString(),
                    TransactionType = row.TransactionType.ToString(),
                    TransactionDate = row.TransactionDate,
                    WeightKgIn = row.WeightKgDelta > 0 ? row.WeightKgDelta : 0m,
                    WeightKgOut = row.WeightKgDelta < 0 ? Math.Abs(row.WeightKgDelta) : 0m,
                    RunningAvailableKg = running,
                    Rate = row.Rate,
                    ValueDelta = row.ValueDelta,
                    Notes = row.Notes
                });
            }
        }

        return new RawMaterialLotReportDto
        {
            From = query.From,
            To = query.To,
            VendorId = query.VendorId,
            ProductId = query.ProductId,
            LotNumber = query.LotNumber,
            OpenOnly = query.OpenOnly,
            Lots = lots.Select(x => new RawMaterialLotRowDto
            {
                LotId = x.Lot.Id,
                LotNumber = x.Lot.LotNumber,
                ProductId = x.Lot.ProductAccountId,
                ProductName = x.ProductName,
                VendorId = x.Lot.VendorAccountId,
                VendorName = x.VendorName,
                SourceVoucherNumber = x.SourceVoucherNumber,
                SourceDate = x.SourceDate,
                OriginalQty = x.Lot.OriginalQty,
                OriginalWeightKg = x.Lot.OriginalWeightKg,
                AvailableWeightKg = VoucherHelpers.Round2(x.AvailableWeightKg),
                ConsumedWeightKg = VoucherHelpers.Round2(x.Lot.OriginalWeightKg - x.AvailableWeightKg),
                BaseRate = x.Lot.BaseRate,
                Status = x.Lot.Status == InventoryLotStatus.Open && x.AvailableWeightKg <= 0.01m
                    ? "Exhausted"
                    : x.Lot.Status.ToString()
            }).ToList(),
            Movements = movements
        };
    }

    private async Task<List<AvailableInventoryLotDto>> GetAvailableLotsAsync(
        Func<IQueryable<Entities.InventoryLot>, IQueryable<Entities.InventoryLot>> filter,
        int? voucherId,
        VoucherType voucherType,
        InventoryTransactionType consumeType)
    {
        var rows = await filter(_db.InventoryLots.AsNoTracking().Where(x => x.Status == InventoryLotStatus.Open))
            .Select(x => new
            {
                x.Id,
                x.LotNumber,
                x.ProductAccountId,
                ProductAccountName = x.ProductAccount.Name,
                x.VendorAccountId,
                VendorName = x.VendorAccount == null ? null : x.VendorAccount.Name,
                SourceVoucherNumber = x.SourceVoucher.VoucherNumber,
                x.SourceVoucherType,
                x.OriginalQty,
                x.OriginalWeightKg,
                x.BaseRate,
                SourceDate = x.SourceVoucher.Date,
                AvailableWeightKg = x.Transactions.Sum(t => t.WeightKgDelta)
            })
            .OrderBy(x => x.SourceDate)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var restoredByLotId = new Dictionary<int, decimal>();
        if (voucherId.HasValue)
        {
            restoredByLotId = await _db.InventoryTransactions
                .AsNoTracking()
                .Where(x => x.VoucherId == voucherId.Value && x.VoucherType == voucherType && x.TransactionType == consumeType)
                .GroupBy(x => x.LotId)
                .Select(g => new
                {
                    g.Key,
                    Kg = g.Sum(x => x.WeightKgDelta < 0 ? -x.WeightKgDelta : x.WeightKgDelta)
                })
                .ToDictionaryAsync(x => x.Key, x => VoucherHelpers.Round2(x.Kg));
        }

        return rows
            .Select(x => new
            {
                x.Id,
                x.LotNumber,
                x.ProductAccountId,
                x.ProductAccountName,
                x.VendorAccountId,
                x.VendorName,
                x.SourceVoucherNumber,
                x.SourceVoucherType,
                x.SourceDate,
                x.OriginalQty,
                x.OriginalWeightKg,
                x.BaseRate,
                AvailableWeightKg = VoucherHelpers.Round2(x.AvailableWeightKg + restoredByLotId.GetValueOrDefault(x.Id))
            })
            .Where(x => x.AvailableWeightKg > 0)
            .Select(x => new AvailableInventoryLotDto
            {
                LotId = x.Id,
                LotNumber = x.LotNumber,
                ProductAccountId = x.ProductAccountId,
                ProductAccountName = x.ProductAccountName,
                VendorId = x.VendorAccountId,
                VendorName = x.VendorName,
                SourceVoucherNumber = x.SourceVoucherNumber,
                SourceVoucherType = x.SourceVoucherType.ToString(),
                SourceDate = x.SourceDate,
                OriginalQty = x.OriginalQty,
                OriginalWeightKg = x.OriginalWeightKg,
                AvailableWeightKg = VoucherHelpers.Round2(x.AvailableWeightKg),
                Rate = x.BaseRate
            })
            .ToList();
    }
}
