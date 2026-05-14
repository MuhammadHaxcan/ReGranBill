using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class ProductStockReportService : IProductStockReportService
{
    private const string InwardDirection = "Inward";
    private const string OutwardDirection = "Outward";
    private const string InvalidDirection = "Invalid";

    private static readonly IReadOnlyDictionary<string, string> AnomalyMessages = new Dictionary<string, string>
    {
        ["InvalidLineSide"] = "Entry has invalid debit/credit sides for movement classification.",
        ["MissingQty"] = "Quantity is missing; bags and kg were treated as zero.",
        ["MissingPackingWeight"] = "Packing weight is missing; kg was treated as zero.",
        ["MissingActualWeightKg"] = "Purchase or purchase return line is missing total entered kg.",
        ["MissingRateOrQtyForValueFallback"] = "Value fallback could not be derived due to missing qty/rate.",
        ["MissingPackingWeightForRbp"] = "RBP=Yes requires packing weight for value fallback.",
        ["NegativeQty"] = "Quantity is negative on a movement line."
    };

    private readonly AppDbContext _db;

    public ProductStockReportService(AppDbContext db) => _db = db;

    public async Task<ProductStockReportDto> GetReportAsync(ProductStockReportQueryDto query)
    {
        var fromDate = query.From;
        var toDate = query.To;

        var entryQuery = _db.JournalEntries
            .AsNoTracking()
            .Where(e => e.Account.AccountType == AccountType.Product
                     || e.Account.AccountType == AccountType.RawMaterial
                     || e.Account.AccountType == AccountType.UnwashedMaterial)
            .Where(e => e.JournalVoucher.RatesAdded);

        if (toDate.HasValue)
            entryQuery = entryQuery.Where(e => e.JournalVoucher.Date <= toDate.Value);

        if (query.CategoryId.HasValue)
            entryQuery = entryQuery.Where(e => e.Account.CategoryId == query.CategoryId.Value);

        if (query.ProductId.HasValue)
            entryQuery = entryQuery.Where(e => e.AccountId == query.ProductId.Value);

        var entries = await entryQuery
            .Select(e => new ProductStockSeed
            {
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherType = e.JournalVoucher.VoucherType.ToString(),
                Date = e.JournalVoucher.Date,
                SortOrder = e.SortOrder,
                ProductId = e.AccountId,
                ProductName = e.Account.Name,
                Packing = e.Account.ProductDetail == null ? null : e.Account.ProductDetail.Packing,
                PackingWeightKg = e.Account.ProductDetail == null ? null : e.Account.ProductDetail.PackingWeightKg,
                Description = e.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                Qty = e.Qty,
                ActualWeightKg = e.ActualWeightKg,
                Rbp = e.Rbp,
                Rate = e.Rate,
                IsEdited = e.IsEdited,
                LineKind = e.LineKind
            })
            .OrderBy(e => e.Date)
            .ThenBy(e => e.VoucherId)
            .ThenBy(e => e.SortOrder)
            .ThenBy(e => e.EntryId)
            .ToListAsync();

        var accumulators = new Dictionary<int, ProductAccumulator>();
        var anomalyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var movementRows = query.IncludeDetails ? new List<ProductStockMovementDto>() : null;

        foreach (var entry in entries)
        {
            if (!accumulators.TryGetValue(entry.ProductId, out var product))
            {
                product = new ProductAccumulator
                {
                    ProductId = entry.ProductId,
                    ProductName = entry.ProductName,
                    Packing = entry.Packing,
                    PackingWeightKg = entry.PackingWeightKg
                };
                accumulators[entry.ProductId] = product;
            }

            var anomalyCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var direction = ResolveDirection(entry, anomalyCodes);
            var hasValidDirection = direction != InvalidDirection;
            var isRbpYes = IsRbpYes(entry.Rbp);

            var qty = entry.Qty ?? 0;
            if (!entry.Qty.HasValue)
                anomalyCodes.Add("MissingQty");

            if (qty < 0)
                anomalyCodes.Add("NegativeQty");

            var packingWeightKg = entry.PackingWeightKg ?? 0m;
            var useActualPurchaseWeight = UsesActualWeight(entry.VoucherType);

            if (useActualPurchaseWeight && (!entry.ActualWeightKg.HasValue || entry.ActualWeightKg.Value <= 0m))
                anomalyCodes.Add("MissingActualWeightKg");

            if (!useActualPurchaseWeight && isRbpYes && Math.Abs(qty) > 0m && packingWeightKg <= 0m)
                anomalyCodes.Add("MissingPackingWeight");

            var bags = isRbpYes ? qty : 0m;
            var weightKg = useActualPurchaseWeight
                ? (entry.ActualWeightKg ?? 0m)
                : isRbpYes
                    ? (packingWeightKg > 0m ? qty * packingWeightKg : 0m)
                    : qty;

            var sideAmount = direction switch
            {
                OutwardDirection => entry.Credit,
                InwardDirection => entry.Debit,
                _ => 0m
            };

            var movementValue = hasValidDirection
                ? (sideAmount > 0m
                    ? sideAmount
                    : DeriveMovementValue(entry, packingWeightKg, anomalyCodes))
                : 0m;

            weightKg = VoucherHelpers.Round2(weightKg);
            movementValue = VoucherHelpers.Round2(movementValue);

            var isOpening = fromDate.HasValue && entry.Date < fromDate.Value;
            var isPeriod = !fromDate.HasValue || entry.Date >= fromDate.Value;

            if (hasValidDirection)
            {
                if (isOpening)
                {
                    ApplyToOpening(product, direction, bags, weightKg, movementValue);
                }
                else if (isPeriod)
                {
                    ApplyToPeriod(product, direction, bags, weightKg, movementValue);
                }
            }

            foreach (var code in anomalyCodes)
            {
                product.AnomalyCount++;
                anomalyCounts[code] = anomalyCounts.TryGetValue(code, out var current)
                    ? current + 1
                    : 1;
            }

            if (query.IncludeDetails && isPeriod)
            {
                movementRows!.Add(new ProductStockMovementDto
                {
                    EntryId = entry.EntryId,
                    VoucherId = entry.VoucherId,
                    VoucherNumber = entry.VoucherNumber,
                    VoucherType = entry.VoucherType,
                    Date = entry.Date,
                    ProductId = entry.ProductId,
                    ProductName = entry.ProductName,
                    Description = entry.Description,
                    Debit = entry.Debit,
                    Credit = entry.Credit,
                    Qty = entry.Qty,
                    Rbp = entry.Rbp,
                    Rate = entry.Rate,
                    WeightKg = weightKg,
                    Value = movementValue,
                    Direction = direction,
                    IsEdited = entry.IsEdited,
                    AnomalyNote = anomalyCodes.Count > 0 ? string.Join(", ", anomalyCodes) : null
                });
            }
        }

        var productRows = accumulators.Values
            .Select(ToRowDto)
            .OrderBy(p => p.ProductName)
            .ToList();

        var totals = BuildTotals(productRows);
        var anomalies = anomalyCounts
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .Select(x => new ProductStockAnomalyDto
            {
                Code = x.Key,
                Message = AnomalyMessages.TryGetValue(x.Key, out var message)
                    ? message
                    : "Data anomaly detected.",
                Count = x.Value
            })
            .ToList();

        return new ProductStockReportDto
        {
            From = fromDate,
            To = toDate,
            CategoryId = query.CategoryId,
            ProductId = query.ProductId,
            IncludeDetails = query.IncludeDetails,
            Totals = totals,
            Products = productRows,
            Movements = movementRows ?? new List<ProductStockMovementDto>(),
            Anomalies = anomalies
        };
    }

    private static string ResolveDirection(ProductStockSeed entry, ISet<string> anomalyCodes)
    {
        if (entry.LineKind is { } kind)
        {
            return kind switch
            {
                ProductionLineKind.Input => OutwardDirection,
                ProductionLineKind.Output => InwardDirection,
                ProductionLineKind.Byproduct => InwardDirection,
                ProductionLineKind.Shortage => OutwardDirection,
                _ => InvalidDirection
            };
        }

        var hasDebit = entry.Debit > 0m;
        var hasCredit = entry.Credit > 0m;

        if (hasCredit && !hasDebit) return OutwardDirection;
        if (hasDebit && !hasCredit) return InwardDirection;

        anomalyCodes.Add("InvalidLineSide");
        return InvalidDirection;
    }

    private static decimal DeriveMovementValue(ProductStockSeed entry, decimal packingWeightKg, ISet<string> anomalyCodes)
    {
        if (!entry.Qty.HasValue || !entry.Rate.HasValue)
        {
            anomalyCodes.Add("MissingRateOrQtyForValueFallback");
            return 0m;
        }

        var qty = entry.Qty.Value;
        var rate = entry.Rate.Value;

        if (UsesActualWeight(entry.VoucherType))
        {
            if (!entry.ActualWeightKg.HasValue || entry.ActualWeightKg.Value <= 0m)
            {
                anomalyCodes.Add("MissingActualWeightKg");
                return 0m;
            }

            return entry.ActualWeightKg.Value * rate;
        }

        if (IsRbpYes(entry.Rbp))
        {
            if (packingWeightKg <= 0m)
            {
                anomalyCodes.Add("MissingPackingWeightForRbp");
                return 0m;
            }

            return qty * packingWeightKg * rate;
        }

        return qty * rate;
    }

    private static bool UsesActualWeight(string voucherType) =>
        string.Equals(voucherType, VoucherType.PurchaseVoucher.ToString(), StringComparison.OrdinalIgnoreCase)
        || string.Equals(voucherType, VoucherType.PurchaseReturnVoucher.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool IsRbpYes(string? rbp) =>
        string.Equals(rbp?.Trim(), "Yes", StringComparison.OrdinalIgnoreCase);

    private static void ApplyToOpening(ProductAccumulator product, string direction, decimal bags, decimal kg, decimal value)
    {
        if (direction == InwardDirection)
        {
            product.OpeningBags += bags;
            product.OpeningKg += kg;
            product.OpeningValue += value;
        }
        else
        {
            product.OpeningBags -= bags;
            product.OpeningKg -= kg;
            product.OpeningValue -= value;
        }
    }

    private static void ApplyToPeriod(ProductAccumulator product, string direction, decimal bags, decimal kg, decimal value)
    {
        if (direction == InwardDirection)
        {
            product.InwardBags += bags;
            product.InwardKg += kg;
            product.InwardValue += value;
        }
        else
        {
            product.OutwardBags += bags;
            product.OutwardKg += kg;
            product.OutwardValue += value;
        }
    }

    private static ProductStockRowDto ToRowDto(ProductAccumulator product)
    {
        var closingBags = product.OpeningBags + product.InwardBags - product.OutwardBags;
        var closingKg = product.OpeningKg + product.InwardKg - product.OutwardKg;
        var closingValue = product.OpeningValue + product.InwardValue - product.OutwardValue;

        return new ProductStockRowDto
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName,
            Packing = string.IsNullOrWhiteSpace(product.Packing) ? null : product.Packing,
            PackingWeightKg = product.PackingWeightKg,
            AnomalyCount = product.AnomalyCount,
            Opening = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(product.OpeningBags),
                Kg = VoucherHelpers.Round2(product.OpeningKg),
                Value = VoucherHelpers.Round2(product.OpeningValue)
            },
            Inward = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(product.InwardBags),
                Kg = VoucherHelpers.Round2(product.InwardKg),
                Value = VoucherHelpers.Round2(product.InwardValue)
            },
            Outward = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(product.OutwardBags),
                Kg = VoucherHelpers.Round2(product.OutwardKg),
                Value = VoucherHelpers.Round2(product.OutwardValue)
            },
            Closing = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(closingBags),
                Kg = VoucherHelpers.Round2(closingKg),
                Value = VoucherHelpers.Round2(closingValue)
            }
        };
    }

    private static ProductStockTotalsDto BuildTotals(IReadOnlyCollection<ProductStockRowDto> productRows)
    {
        return new ProductStockTotalsDto
        {
            ProductCount = productRows.Count,
            AnomalyCount = productRows.Sum(p => p.AnomalyCount),
            Opening = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(productRows.Sum(p => p.Opening.Bags)),
                Kg = VoucherHelpers.Round2(productRows.Sum(p => p.Opening.Kg)),
                Value = VoucherHelpers.Round2(productRows.Sum(p => p.Opening.Value))
            },
            Inward = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(productRows.Sum(p => p.Inward.Bags)),
                Kg = VoucherHelpers.Round2(productRows.Sum(p => p.Inward.Kg)),
                Value = VoucherHelpers.Round2(productRows.Sum(p => p.Inward.Value))
            },
            Outward = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(productRows.Sum(p => p.Outward.Bags)),
                Kg = VoucherHelpers.Round2(productRows.Sum(p => p.Outward.Kg)),
                Value = VoucherHelpers.Round2(productRows.Sum(p => p.Outward.Value))
            },
            Closing = new ProductStockMetricDto
            {
                Bags = VoucherHelpers.Round2(productRows.Sum(p => p.Closing.Bags)),
                Kg = VoucherHelpers.Round2(productRows.Sum(p => p.Closing.Kg)),
                Value = VoucherHelpers.Round2(productRows.Sum(p => p.Closing.Value))
            }
        };
    }

    private sealed class ProductStockSeed
    {
        public int EntryId { get; set; }
        public int VoucherId { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public int SortOrder { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Packing { get; set; }
        public decimal? PackingWeightKg { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int? Qty { get; set; }
        public decimal? ActualWeightKg { get; set; }
        public string? Rbp { get; set; }
        public decimal? Rate { get; set; }
        public bool IsEdited { get; set; }
        public ProductionLineKind? LineKind { get; set; }
    }

    private sealed class ProductAccumulator
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Packing { get; set; }
        public decimal? PackingWeightKg { get; set; }
        public decimal OpeningBags { get; set; }
        public decimal OpeningKg { get; set; }
        public decimal OpeningValue { get; set; }
        public decimal InwardBags { get; set; }
        public decimal InwardKg { get; set; }
        public decimal InwardValue { get; set; }
        public decimal OutwardBags { get; set; }
        public decimal OutwardKg { get; set; }
        public decimal OutwardValue { get; set; }
        public int AnomalyCount { get; set; }
    }
}
