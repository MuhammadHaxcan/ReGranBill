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
                (v.VoucherType == VoucherType.SaleVoucher || v.VoucherType == VoucherType.PurchaseVoucher));

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

            foreach (var entry in productEntries)
            {
                if (productId.HasValue && entry.AccountId != productId.Value)
                {
                    continue;
                }

                var packingWeight = entry.Account?.ProductDetail?.PackingWeightKg ?? 0m;
                var quantity = entry.Qty ?? 0;
                var isPurchaseLine = voucher.VoucherType == VoucherType.PurchaseVoucher;
                var isPacked = isPurchaseLine || string.Equals(entry.Rbp, "Yes", StringComparison.OrdinalIgnoreCase);
                var totalWeight = isPurchaseLine
                    ? Round2(entry.ActualWeightKg ?? 0m)
                    : Round2(isPacked ? packingWeight * quantity : quantity);

                rows.Add(new SalePurchaseReportRowDto
                {
                    VoucherId = voucher.Id,
                    VoucherNumber = voucher.VoucherNumber,
                    VoucherType = voucher.VoucherType.ToString(),
                    Date = voucher.Date,
                    ProductId = entry.AccountId,
                    ProductName = entry.Account?.Name ?? "-",
                    Packing = entry.Account?.ProductDetail?.Packing,
                    Unit = entry.Account?.ProductDetail?.Unit,
                    Rbp = isPacked ? "Yes" : "No",
                    Qty = quantity,
                    PackingWeightKg = packingWeight,
                    TotalWeightKg = totalWeight,
                    DisplayQuantity = BuildDisplayQuantity(quantity, totalWeight, isPacked),
                    FromName = voucher.VoucherType == VoucherType.SaleVoucher ? CompanyName : partyName,
                    ToName = voucher.VoucherType == VoucherType.SaleVoucher ? partyName : CompanyName,
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
            TotalPackedBags = rows
                .Where(row => row.Rbp == "Yes")
                .Sum(row => row.Qty),
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

        throw new RequestValidationException("Type must be All, Sale, or Purchase.");
    }

    private static bool IsInventoryAccount(Account? account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static string BuildDisplayQuantity(int qty, decimal totalWeightKg, bool isPacked) =>
        isPacked
            ? $"{qty} bags / {FormatDecimal(totalWeightKg)} kg"
            : $"{FormatDecimal(totalWeightKg)} kg";

    private static string FormatDecimal(decimal value) =>
        value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString("0")
            : value.ToString("0.##");

    private static decimal Round2(decimal value) =>
        VoucherHelpers.Round2(value);
}
