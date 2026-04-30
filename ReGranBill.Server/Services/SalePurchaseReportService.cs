using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.SalePurchaseReport;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class SalePurchaseReportService : ISalePurchaseReportService
{
    private const string CompanyName = "KPI";
    private const string OutsiderLabel = "Outsider";

    private readonly AppDbContext _db;

    public SalePurchaseReportService(AppDbContext db) => _db = db;

    public async Task<SalePurchaseReportDto> GetReportAsync(DateOnly? from, DateOnly? to, string? type, int? productId)
    {
        var fromDate = VoucherHelpers.ToUtcStartOfDay(from);
        var toExclusiveDate = VoucherHelpers.ToUtcStartOfDay(to?.AddDays(1));
        var voucherFilter = ParseVoucherFilter(type);

        var voucherQuery = _db.JournalVouchers
            .AsNoTracking()
            .Include(v => v.Entries)
                .ThenInclude(e => e.Account)
                    .ThenInclude(a => a.ProductDetail)
            .Include(v => v.Entries)
                .ThenInclude(e => e.Account)
                    .ThenInclude(a => a.PartyDetail)
            .Where(v =>
                v.RatesAdded &&
                (v.VoucherType == VoucherType.SaleVoucher
                    || v.VoucherType == VoucherType.PurchaseVoucher
                    || v.VoucherType == VoucherType.SaleReturnVoucher
                    || v.VoucherType == VoucherType.PurchaseReturnVoucher));

        if (fromDate.HasValue)
        {
            voucherQuery = voucherQuery.Where(v => v.Date >= fromDate.Value);
        }

        if (toExclusiveDate.HasValue)
        {
            voucherQuery = voucherQuery.Where(v => v.Date < toExclusiveDate.Value);
        }

        if (voucherFilter.HasValue)
        {
            voucherQuery = voucherQuery.Where(v => v.VoucherType == voucherFilter.Value);
        }

        var vouchers = await voucherQuery
            .OrderByDescending(v => v.Date)
            .ThenByDescending(v => v.Id)
            .ToListAsync();

        var voucherIds = vouchers.Select(v => v.Id).ToList();
        var cartageTransporters = await _db.JournalVoucherReferences
            .AsNoTracking()
            .Where(reference => voucherIds.Contains(reference.MainVoucherId))
            .Include(reference => reference.ReferenceVoucher)
                .ThenInclude(voucher => voucher.Entries)
                    .ThenInclude(entry => entry.Account)
            .ToDictionaryAsync(
                reference => reference.MainVoucherId,
                reference => reference.ReferenceVoucher.Entries
                    .FirstOrDefault(entry => entry.SortOrder == 1 && entry.Credit > 0)?.Account?.Name);

        var rows = new List<SalePurchaseReportRowDto>();

        foreach (var voucher in vouchers)
        {
            var partyEntry = voucher.Entries.FirstOrDefault(entry => entry.SortOrder == 0);
            var partyName = partyEntry?.Account?.Name ?? "-";
            var transporterName = cartageTransporters.GetValueOrDefault(voucher.Id);
            var groupLabel = string.IsNullOrWhiteSpace(transporterName) ? OutsiderLabel : transporterName;

            var productEntries = voucher.Entries
                .Where(entry => entry.SortOrder > 0 && IsInventoryAccount(entry.Account))
                .OrderBy(entry => entry.SortOrder);

            foreach (var entryGroup in productEntries.GroupBy(entry => entry.AccountId))
            {
                if (productId.HasValue && entryGroup.Key != productId.Value)
                {
                    continue;
                }

                var isPurchaseLine = voucher.VoucherType == VoucherType.PurchaseVoucher;
                var isPurchaseReturnLine = voucher.VoucherType == VoucherType.PurchaseReturnVoucher;
                var sampleEntry = entryGroup.First();
                var packingWeight = sampleEntry.Account?.ProductDetail?.PackingWeightKg ?? 0m;

                var packedBags = 0;
                decimal looseWeightKg = 0m;
                decimal totalWeightKg = 0m;

                foreach (var entry in entryGroup)
                {
                    var quantity = entry.Qty ?? 0;
                    var isPacked = isPurchaseLine
                        || string.Equals(entry.Rbp, "Yes", StringComparison.OrdinalIgnoreCase);
                    var lineWeight = isPurchaseLine || isPurchaseReturnLine
                        ? entry.ActualWeightKg ?? 0m
                        : isPacked
                            ? packingWeight * quantity
                            : quantity;

                    totalWeightKg += lineWeight;
                    if (isPacked)
                    {
                        packedBags += quantity;
                    }
                    else
                    {
                        looseWeightKg += lineWeight;
                    }
                }

                totalWeightKg = Round2(totalWeightKg);
                looseWeightKg = Round2(looseWeightKg);

                rows.Add(new SalePurchaseReportRowDto
                {
                    VoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    VoucherType = voucher.VoucherType.ToString(),
                    Date = voucher.Date,
                    ProductId = sampleEntry.AccountId,
                    ProductName = sampleEntry.Account?.Name ?? "-",
                    Packing = sampleEntry.Account?.ProductDetail?.Packing,
                    Rbp = looseWeightKg > 0 && packedBags > 0 ? "Mixed" : (packedBags > 0 ? "Yes" : "No"),
                    Qty = packedBags,
                    LooseWeightKg = looseWeightKg,
                    PackingWeightKg = packingWeight,
                    TotalWeightKg = totalWeightKg,
                    DisplayQuantity = BuildDisplayQuantity(packedBags, totalWeightKg, looseWeightKg),
                    FromName = voucher.VoucherType == VoucherType.SaleVoucher ? CompanyName
                        : voucher.VoucherType == VoucherType.SaleReturnVoucher ? partyName
                        : voucher.VoucherType == VoucherType.PurchaseReturnVoucher ? partyName
                        : partyName,
                    ToName = voucher.VoucherType == VoucherType.SaleVoucher ? partyName
                        : voucher.VoucherType == VoucherType.SaleReturnVoucher ? CompanyName
                        : voucher.VoucherType == VoucherType.PurchaseReturnVoucher ? CompanyName
                        : CompanyName,
                    TransporterName = transporterName,
                    GroupLabel = groupLabel,
                    GroupSortDate = voucher.Date
                });
            }
        }

        var groupLatestDates = rows
            .GroupBy(row => row.GroupLabel)
            .ToDictionary(group => group.Key, group => group.Max(row => row.Date));

        foreach (var row in rows)
        {
            row.GroupSortDate = groupLatestDates[row.GroupLabel];
        }

        rows = rows
            .OrderByDescending(row => row.GroupSortDate)
            .ThenBy(row => row.GroupLabel)
            .ThenByDescending(row => row.Date)
            .ThenByDescending(row => row.VoucherId)
            .ThenBy(row => row.ProductId)
            .ToList();

        return new SalePurchaseReportDto
        {
            TotalRows = rows.Count,
            TotalSaleRows = rows.Count(row => row.VoucherType == VoucherType.SaleVoucher.ToString()),
            TotalPurchaseRows = rows.Count(row => row.VoucherType == VoucherType.PurchaseVoucher.ToString()),
            TotalSaleReturnRows = rows.Count(row => row.VoucherType == VoucherType.SaleReturnVoucher.ToString()),
            TotalPurchaseReturnRows = rows.Count(row => row.VoucherType == VoucherType.PurchaseReturnVoucher.ToString()),
            TotalPackedBags = rows.Sum(row => row.Qty),
            TotalWeightKg = Round2(rows.Sum(row => row.TotalWeightKg)),
            Rows = rows
        };
    }

    private static VoucherType? ParseVoucherFilter(string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(type, "Sale", StringComparison.OrdinalIgnoreCase))
        {
            return VoucherType.SaleVoucher;
        }

        if (string.Equals(type, "Purchase", StringComparison.OrdinalIgnoreCase))
        {
            return VoucherType.PurchaseVoucher;
        }

        if (string.Equals(type, "SaleReturn", StringComparison.OrdinalIgnoreCase))
        {
            return VoucherType.SaleReturnVoucher;
        }

        if (string.Equals(type, "PurchaseReturn", StringComparison.OrdinalIgnoreCase))
        {
            return VoucherType.PurchaseReturnVoucher;
        }

        throw new RequestValidationException("Type must be All, Sale, Purchase, SaleReturn, or PurchaseReturn.");
    }

    private static bool IsInventoryAccount(Account? account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static string BuildDisplayQuantity(int packedBags, decimal totalWeightKg, decimal looseWeightKg)
    {
        if (packedBags > 0 && looseWeightKg > 0)
            return $"{packedBags} bags / {FormatDecimal(totalWeightKg)} kg (Loose {FormatDecimal(looseWeightKg)} kg)";

        if (packedBags > 0)
            return $"{packedBags} bags / {FormatDecimal(totalWeightKg)} kg";

        return $"{FormatDecimal(totalWeightKg)} kg (Loose {FormatDecimal(looseWeightKg)} kg)";
    }

    private static string FormatDecimal(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString("0")
            : value.ToString("0.##");

    private static decimal Round2(decimal value) =>
        VoucherHelpers.Round2(value);
}
