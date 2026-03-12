using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.ProductStockReport;
using ReGranBill.Server.Enums;

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
        ["MissingRateOrQtyForValueFallback"] = "Value fallback could not be derived due to missing qty/rate.",
        ["MissingPackingWeightForRbp"] = "RBP=Yes requires packing weight for value fallback.",
        ["NegativeQty"] = "Quantity is negative on a movement line."
    };

    private readonly AppDbContext _db;

    public ProductStockReportService(AppDbContext db) => _db = db;

    public async Task<ProductStockReportDto> GetReportAsync(ProductStockReportQueryDto query)
    {
        var normalizedFrom = query.From;
        var normalizedTo = query.To;
        var fromDate = ToUtcStartOfDay(normalizedFrom);
        var toExclusiveDate = ToUtcStartOfDay(normalizedTo?.AddDays(1));

        var entryQuery = _db.JournalEntries
            .AsNoTracking()
            .Where(e => e.Account.AccountType == AccountType.Product)
            .Where(e => e.JournalVoucher.RatesAdded);

        if (toExclusiveDate.HasValue)
            entryQuery = entryQuery.Where(e => e.JournalVoucher.Date < toExclusiveDate.Value);

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
                Packing = e.Account.ProductDetail != null ? e.Account.ProductDetail.Packing : null,
                PackingWeightKg = e.Account.ProductDetail != null ? e.Account.ProductDetail.PackingWeightKg : null,
                Description = e.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                Qty = e.Qty,
                Rbp = e.Rbp,
                Rate = e.Rate,
                IsEdited = e.IsEdited
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
            if (isRbpYes && Math.Abs(qty) > 0m && packingWeightKg <= 0m)
                anomalyCodes.Add("MissingPackingWeight");

            var bags = isRbpYes ? qty : 0m;
            var weightKg = isRbpYes
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

            weightKg = Round2(weightKg);
            movementValue = Round2(movementValue);

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
                    product.MovementCount++;
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
            From = normalizedFrom,
            To = normalizedTo,
            CategoryId = query.CategoryId,
            ProductId = query.ProductId,
            IncludeDetails = query.IncludeDetails,
            Totals = totals,
            Products = productRows,
            Movements = movementRows ?? new List<ProductStockMovementDto>(),
            Anomalies = anomalies
        };
    }

    private static DateTime? ToUtcStartOfDay(DateOnly? value) =>
        value.HasValue
            ? DateTime.SpecifyKind(value.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : null;

    private static string ResolveDirection(ProductStockSeed entry, ISet<string> anomalyCodes)
    {
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
            MovementCount = product.MovementCount,
            AnomalyCount = product.AnomalyCount,
            Opening = new ProductStockMetricDto
            {
                Bags = Round2(product.OpeningBags),
                Kg = Round2(product.OpeningKg),
                Value = Round2(product.OpeningValue)
            },
            Inward = new ProductStockMetricDto
            {
                Bags = Round2(product.InwardBags),
                Kg = Round2(product.InwardKg),
                Value = Round2(product.InwardValue)
            },
            Outward = new ProductStockMetricDto
            {
                Bags = Round2(product.OutwardBags),
                Kg = Round2(product.OutwardKg),
                Value = Round2(product.OutwardValue)
            },
            Closing = new ProductStockMetricDto
            {
                Bags = Round2(closingBags),
                Kg = Round2(closingKg),
                Value = Round2(closingValue)
            }
        };
    }

    private static ProductStockTotalsDto BuildTotals(IReadOnlyCollection<ProductStockRowDto> productRows)
    {
        return new ProductStockTotalsDto
        {
            ProductCount = productRows.Count,
            MovementCount = productRows.Sum(p => p.MovementCount),
            AnomalyCount = productRows.Sum(p => p.AnomalyCount),
            Opening = new ProductStockMetricDto
            {
                Bags = Round2(productRows.Sum(p => p.Opening.Bags)),
                Kg = Round2(productRows.Sum(p => p.Opening.Kg)),
                Value = Round2(productRows.Sum(p => p.Opening.Value))
            },
            Inward = new ProductStockMetricDto
            {
                Bags = Round2(productRows.Sum(p => p.Inward.Bags)),
                Kg = Round2(productRows.Sum(p => p.Inward.Kg)),
                Value = Round2(productRows.Sum(p => p.Inward.Value))
            },
            Outward = new ProductStockMetricDto
            {
                Bags = Round2(productRows.Sum(p => p.Outward.Bags)),
                Kg = Round2(productRows.Sum(p => p.Outward.Kg)),
                Value = Round2(productRows.Sum(p => p.Outward.Value))
            },
            Closing = new ProductStockMetricDto
            {
                Bags = Round2(productRows.Sum(p => p.Closing.Bags)),
                Kg = Round2(productRows.Sum(p => p.Closing.Kg)),
                Value = Round2(productRows.Sum(p => p.Closing.Value))
            }
        };
    }

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class ProductStockSeed
    {
        public int EntryId { get; set; }
        public int VoucherId { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int SortOrder { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Packing { get; set; }
        public decimal? PackingWeightKg { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int? Qty { get; set; }
        public string? Rbp { get; set; }
        public decimal? Rate { get; set; }
        public bool IsEdited { get; set; }
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
        public int MovementCount { get; set; }
        public int AnomalyCount { get; set; }
    }
}
