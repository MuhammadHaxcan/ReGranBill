using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.ProductionVouchers;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class ProductionVoucherService : IProductionVoucherService
{
    private const decimal MassBalanceTolerance = 0.01m;

    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public ProductionVoucherService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<ProductionVoucherListDto>> GetAllAsync()
    {
        var vouchers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.ProductionVoucher)
            .Include(j => j.Entries)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return vouchers.Select(v =>
        {
            var entries = v.Entries.ToList();
            return new ProductionVoucherListDto
            {
                Id = v.Id,
                VoucherNumber = v.VoucherNumber,
                Date = v.Date,
                Description = v.Description,
                LotNumber = v.VehicleNumber,
                TotalInputKg = SumKg(entries, ProductionLineKind.Input),
                TotalOutputKg = SumKg(entries, ProductionLineKind.Output),
                TotalByproductKg = SumKg(entries, ProductionLineKind.Byproduct),
                ShortageKg = SumKg(entries, ProductionLineKind.Shortage),
                CreatedAt = v.CreatedAt
            };
        }).ToList();
    }

    public async Task<ProductionVoucherDto?> GetByIdAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.ProductionVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.VendorAccount)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        return MapToDto(voucher);
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.ProductionVoucher, "PRD-");

    public async Task<ProductionVoucherDto> CreateAsync(CreateProductionVoucherRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var voucherNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.ProductionVoucher, "PRD-");

        var voucher = new JournalVoucher
        {
            VoucherNumber = voucherNumber,
            Date = request.Date,
            VoucherType = VoucherType.ProductionVoucher,
            VehicleNumber = VoucherHelpers.ToNullIfWhiteSpace(request.LotNumber),
            Description = BuildDescription(request, validation),
            RatesAdded = true,
            CreatedBy = userId
        };

        AppendEntries(voucher, request, validation, isEdited: false);

        _db.JournalVouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<ProductionVoucherDto?> UpdateAsync(int id, CreateProductionVoucherRequest request)
    {
        var voucher = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.ProductionVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        voucher.Date = request.Date;
        voucher.VehicleNumber = VoucherHelpers.ToNullIfWhiteSpace(request.LotNumber);
        voucher.Description = BuildDescription(request, validation);
        voucher.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();

        AppendEntries(voucher, request, validation, isEdited: true);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.ProductionVoucher);

        if (voucher == null) return (false, null);

        voucher.IsDeleted = true;
        voucher.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<LatestPurchaseRateDto>> GetLatestPurchaseRatesAsync(int vendorId, IReadOnlyCollection<int> accountIds)
    {
        if (vendorId <= 0) return [];

        var ids = accountIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0) return [];

        // Source 1: direct PurchaseVoucher of this account from this vendor.
        // PurchaseVoucher convention: party (vendor) line has SortOrder=0; product lines have SortOrder>0.
        var purchaseRates = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                e.SortOrder > 0 &&
                ids.Contains(e.AccountId) &&
                e.Rate.HasValue &&
                e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.PurchaseVoucher &&
                e.JournalVoucher.Entries.Any(p => p.SortOrder == 0 && p.AccountId == vendorId))
            .Select(e => new RateCandidate
            {
                AccountId = e.AccountId,
                Rate = e.Rate!.Value,
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherDate = e.JournalVoucher.Date
            })
            .ToListAsync();

        // Source 2: WashingVoucher Dr Washed lines stamped with this vendor (lineage).
        // This lets a washed RawMaterial bought-as-unwashed-from-vendor-A show up under (A, washed)
        // even though A never directly sold the washed account.
        var washingRates = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                ids.Contains(e.AccountId) &&
                e.Debit > 0 &&
                e.VendorAccountId == vendorId &&
                e.Rate.HasValue &&
                e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.WashingVoucher)
            .Select(e => new RateCandidate
            {
                AccountId = e.AccountId,
                Rate = e.Rate!.Value,
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherDate = e.JournalVoucher.Date
            })
            .ToListAsync();

        return purchaseRates
            .Concat(washingRates)
            .OrderByDescending(c => c.VoucherDate)
            .ThenByDescending(c => c.VoucherId)
            .ThenByDescending(c => c.EntryId)
            .GroupBy(c => c.AccountId)
            .Select(g => g.First())
            .Select(c => new LatestPurchaseRateDto
            {
                AccountId = c.AccountId,
                Rate = c.Rate,
                SourceVoucherNumber = c.VoucherNumber,
                SourceDate = c.VoucherDate
            })
            .ToList();
    }

    private sealed class RateCandidate
    {
        public int AccountId { get; set; }
        public decimal Rate { get; set; }
        public int EntryId { get; set; }
        public int VoucherId { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public DateOnly VoucherDate { get; set; }
    }

    private static void AppendEntries(JournalVoucher voucher, CreateProductionVoucherRequest request, ValidatedRequest validation, bool isEdited)
    {
        var sortOrder = 0;

        foreach (var line in request.Inputs.OrderBy(l => l.SortOrder))
        {
            var account = validation.Accounts[line.AccountId];
            var weight = VoucherHelpers.Round2(line.WeightKg);
            var rate = line.Rate!.Value;
            var amount = VoucherHelpers.Round2(weight * rate);
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description)
                    ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg (vendor: {validation.VendorAccounts[line.VendorId!.Value].Name})",
                Debit = 0,
                Credit = amount,
                Qty = line.Qty,
                ActualWeightKg = weight,
                Rbp = "Yes",
                Rate = rate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Input,
                VendorAccountId = line.VendorId,
                SortOrder = ++sortOrder
            });
        }

        var derivedRate = validation.DerivedRate;

        foreach (var line in request.Outputs.OrderBy(l => l.SortOrder))
        {
            var account = validation.Accounts[line.AccountId];
            var weight = VoucherHelpers.Round2(line.WeightKg);
            var amount = VoucherHelpers.Round2(weight * derivedRate);
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description)
                    ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg",
                Debit = amount,
                Credit = 0,
                Qty = line.Qty,
                ActualWeightKg = weight,
                Rbp = "Yes",
                Rate = derivedRate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Output,
                SortOrder = ++sortOrder
            });
        }

        foreach (var line in request.Byproducts.OrderBy(l => l.SortOrder))
        {
            var account = validation.Accounts[line.AccountId];
            var weight = VoucherHelpers.Round2(line.WeightKg);
            var amount = VoucherHelpers.Round2(weight * derivedRate);
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description)
                    ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg",
                Debit = amount,
                Credit = 0,
                Qty = line.Qty,
                ActualWeightKg = weight,
                Rbp = "Yes",
                Rate = derivedRate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Byproduct,
                SortOrder = ++sortOrder
            });
        }

        if (request.Shortage != null && request.Shortage.WeightKg > 0)
        {
            var shortageAccount = validation.Accounts[request.Shortage.AccountId];
            var shortageKg = VoucherHelpers.Round2(request.Shortage.WeightKg);
            var shortageAmount = VoucherHelpers.Round2(shortageKg * derivedRate);
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = shortageAccount.Id,
                Description = $"Production shortage / loss - {shortageKg:0.##} kg",
                Debit = shortageAmount,
                Credit = 0,
                Qty = null,
                ActualWeightKg = shortageKg,
                Rbp = "Yes",
                Rate = derivedRate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Shortage,
                SortOrder = ++sortOrder
            });
        }
    }

    private static string? BuildDescription(CreateProductionVoucherRequest request, ValidatedRequest validation)
    {
        var trimmed = VoucherHelpers.ToNullIfWhiteSpace(request.Description);
        if (trimmed != null) return trimmed;

        var inputNames = string.Join(", ", request.Inputs
            .OrderBy(l => l.SortOrder)
            .Select(l => validation.Accounts.TryGetValue(l.AccountId, out var a) ? a.Name : $"#{l.AccountId}"));
        var outputNames = string.Join(", ", request.Outputs
            .OrderBy(l => l.SortOrder)
            .Select(l => validation.Accounts.TryGetValue(l.AccountId, out var a) ? a.Name : $"#{l.AccountId}"));

        if (string.IsNullOrEmpty(inputNames) && string.IsNullOrEmpty(outputNames)) return null;
        return $"Production: {inputNames} -> {outputNames}";
    }

    private async Task<ValidatedRequest> ValidateRequestAsync(CreateProductionVoucherRequest request)
    {
        if (request.Inputs.Count == 0)
            throw new RequestValidationException("Add at least one input line.");
        if (request.Outputs.Count == 0)
            throw new RequestValidationException("Add at least one output line.");

        ValidateLines(request.Inputs, "input", requireVendorAndRate: true);
        ValidateLines(request.Outputs, "output", requireVendorAndRate: false);
        ValidateLines(request.Byproducts, "byproduct", requireVendorAndRate: false);

        var shortageKg = 0m;
        if (request.Shortage != null)
        {
            if (request.Shortage.WeightKg < 0)
                throw new RequestValidationException("Shortage weight cannot be negative.");
            if (request.Shortage.WeightKg > 0 && request.Shortage.AccountId <= 0)
                throw new RequestValidationException("Select a valid Production Loss account for the shortage.");
            shortageKg = request.Shortage.WeightKg;
        }

        var totalInputKg = request.Inputs.Sum(l => l.WeightKg);
        var totalOutputKg = request.Outputs.Sum(l => l.WeightKg);
        var totalByproductKg = request.Byproducts.Sum(l => l.WeightKg);
        var rhs = totalOutputKg + totalByproductKg + shortageKg;
        var diff = totalInputKg - rhs;
        if (Math.Abs(diff) > MassBalanceTolerance)
        {
            throw new RequestValidationException(
                $"Mass balance failed. Inputs {totalInputKg:0.##} kg must equal outputs + byproducts + shortage ({rhs:0.##} kg). Difference: {diff:0.##} kg.");
        }

        var accountIds = request.Inputs.Select(l => l.AccountId)
            .Concat(request.Outputs.Select(l => l.AccountId))
            .Concat(request.Byproducts.Select(l => l.AccountId))
            .Concat(request.Shortage != null && request.Shortage.WeightKg > 0 ? new[] { request.Shortage.AccountId } : Array.Empty<int>())
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var accounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        foreach (var line in request.Inputs)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account)
                || (account.AccountType != AccountType.RawMaterial && account.AccountType != AccountType.Product))
            {
                throw new RequestValidationException("Each input must be a valid Raw Material or Product account.");
            }
        }

        foreach (var line in request.Outputs.Concat(request.Byproducts))
        {
            if (!accounts.TryGetValue(line.AccountId, out var account)
                || account.AccountType != AccountType.Product)
            {
                throw new RequestValidationException("Each output / byproduct must be a valid Product account.");
            }
        }

        if (request.Shortage != null && request.Shortage.WeightKg > 0)
        {
            if (!accounts.TryGetValue(request.Shortage.AccountId, out var account)
                || account.AccountType != AccountType.Expense)
            {
                throw new RequestValidationException("Shortage account must be an Expense account (e.g., Production Loss).");
            }
        }

        var vendorIds = request.Inputs
            .Where(l => l.VendorId.HasValue && l.VendorId.Value > 0)
            .Select(l => l.VendorId!.Value)
            .Distinct()
            .ToList();

        var vendorAccounts = await _db.Accounts
            .Where(a => vendorIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        foreach (var line in request.Inputs)
        {
            if (!vendorAccounts.TryGetValue(line.VendorId!.Value, out var vendor)
                || vendor.AccountType != AccountType.Party)
            {
                throw new RequestValidationException("Each input line must specify a valid vendor (Party) account.");
            }
        }

        // Cost basis: total input cost = Σ(input.weight × input.rate).
        // Derived per-kg rate distributes that cost uniformly across all physical mass leaving the
        // production (outputs + byproducts + shortage), so Σ Debit == Σ Credit by construction.
        var totalInputCost = request.Inputs.Sum(l => VoucherHelpers.Round2(l.WeightKg) * l.Rate!.Value);
        var totalPhysicalOutKg = VoucherHelpers.Round2(totalOutputKg + totalByproductKg + shortageKg);
        var derivedRate = totalPhysicalOutKg > 0
            ? VoucherHelpers.Round2(totalInputCost / totalPhysicalOutKg)
            : 0m;

        return new ValidatedRequest(accounts, vendorAccounts, derivedRate, VoucherHelpers.Round2(totalInputCost));
    }

    private static void ValidateLines(IList<ProductionLineRequest> lines, string label, bool requireVendorAndRate)
    {
        foreach (var line in lines)
        {
            if (line.AccountId <= 0)
                throw new RequestValidationException($"Each {label} line must reference a valid account.");
            if (line.Qty < 0)
                throw new RequestValidationException($"{label} bags cannot be negative.");
            if (line.WeightKg <= 0)
                throw new RequestValidationException($"{label} weight must be greater than zero.");

            if (requireVendorAndRate)
            {
                if (!line.VendorId.HasValue || line.VendorId.Value <= 0)
                    throw new RequestValidationException($"Each {label} line must select a vendor.");
                if (!line.Rate.HasValue || line.Rate.Value <= 0)
                    throw new RequestValidationException($"Each {label} line must have a rate greater than zero.");
            }
        }
    }

    private static decimal SumKg(IEnumerable<JournalEntry> entries, ProductionLineKind kind) =>
        VoucherHelpers.Round2(entries.Where(e => e.LineKind == kind).Sum(e => e.ActualWeightKg ?? 0m));

    private static ProductionVoucherDto MapToDto(JournalVoucher voucher)
    {
        var entries = voucher.Entries.OrderBy(e => e.SortOrder).ToList();

        ProductionLineDto LineDto(JournalEntry e) => new()
        {
            Id = e.Id,
            AccountId = e.AccountId,
            AccountName = e.Account?.Name,
            Packing = e.Account?.ProductDetail?.Packing,
            PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg,
            Qty = e.Qty ?? 0,
            WeightKg = e.ActualWeightKg ?? 0,
            Description = e.Description,
            SortOrder = e.SortOrder,
            VendorId = e.VendorAccountId,
            VendorName = e.VendorAccount?.Name,
            Rate = e.Rate
        };

        var inputs = entries.Where(e => e.LineKind == ProductionLineKind.Input).Select(LineDto).ToList();
        var outputs = entries.Where(e => e.LineKind == ProductionLineKind.Output).Select(LineDto).ToList();
        var byproducts = entries.Where(e => e.LineKind == ProductionLineKind.Byproduct).Select(LineDto).ToList();
        var shortageEntry = entries.FirstOrDefault(e => e.LineKind == ProductionLineKind.Shortage);

        var totalInputCost = VoucherHelpers.Round2(inputs.Sum(l => l.WeightKg * (l.Rate ?? 0m)));
        var firstOutputRate = outputs.FirstOrDefault()?.Rate ?? byproducts.FirstOrDefault()?.Rate ?? shortageEntry?.Rate ?? 0m;

        return new ProductionVoucherDto
        {
            Id = voucher.Id,
            VoucherNumber = voucher.VoucherNumber,
            Date = voucher.Date,
            LotNumber = voucher.VehicleNumber,
            Description = voucher.Description,
            VoucherType = voucher.VoucherType.ToString(),
            Inputs = inputs,
            Outputs = outputs,
            Byproducts = byproducts,
            Shortage = shortageEntry == null ? null : new ProductionShortageDto
            {
                Id = shortageEntry.Id,
                AccountId = shortageEntry.AccountId,
                AccountName = shortageEntry.Account?.Name,
                WeightKg = shortageEntry.ActualWeightKg ?? 0,
                Rate = shortageEntry.Rate
            },
            TotalInputKg = VoucherHelpers.Round2(inputs.Sum(l => l.WeightKg)),
            TotalOutputKg = VoucherHelpers.Round2(outputs.Sum(l => l.WeightKg)),
            TotalByproductKg = VoucherHelpers.Round2(byproducts.Sum(l => l.WeightKg)),
            ShortageKg = VoucherHelpers.Round2(shortageEntry?.ActualWeightKg ?? 0),
            TotalInputCost = totalInputCost,
            DerivedOutputRate = firstOutputRate
        };
    }

    private sealed record ValidatedRequest(
        IReadOnlyDictionary<int, Account> Accounts,
        IReadOnlyDictionary<int, Account> VendorAccounts,
        decimal DerivedRate,
        decimal TotalInputCost);
}
