using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.WashingVouchers;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class WashingVoucherService : IWashingVoucherService
{
    private const decimal DefaultWastageThresholdPct = 10m;
    private const decimal MassBalanceTolerance = 0.01m;

    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public WashingVoucherService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.WashingVoucher, "WSH-");

    public async Task<List<WashingVoucherListDto>> GetAllAsync()
    {
        var vouchers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.WashingVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account)
            .Include(j => j.Entries).ThenInclude(e => e.VendorAccount)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return vouchers.Select(MapToListDto).ToList();
    }

    public async Task<WashingVoucherDto?> GetByIdAsync(int id)
    {
        return await GetVoucherAsync(j => j.Id == id);
    }

    public async Task<WashingVoucherDto?> GetByNumberAsync(string voucherNumber)
    {
        var normalized = voucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return await GetVoucherAsync(j => j.VoucherNumber == normalized);
    }

    public async Task<WashingVoucherDto> CreateAsync(CreateWashingVoucherRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var voucherNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.WashingVoucher, "WSH-");

        var voucher = new JournalVoucher
        {
            VoucherNumber = voucherNumber,
            Date = request.Date,
            VoucherType = VoucherType.WashingVoucher,
            VehicleNumber = null,
            Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description) ?? BuildDescription(validation),
            RatesAdded = true,
            CreatedBy = userId
        };

        var thresholdRatio = validation.ThresholdPct / 100m;
        var inputKg = VoucherHelpers.Round2(request.InputWeightKg);
        var outputKg = VoucherHelpers.Round2(validation.OutputLines.Sum(line => line.WeightKg));
        var wastageKg = VoucherHelpers.Round2(inputKg - outputKg);
        var sourceRate = validation.SourceRate;
        var inputCost = VoucherHelpers.Round2(inputKg * sourceRate);
        var excessWastageKg = VoucherHelpers.Round2(Math.Max(0m, wastageKg - thresholdRatio * inputKg));
        var excessValue = VoucherHelpers.Round2(excessWastageKg * sourceRate);
        var washedCost = VoucherHelpers.Round2(inputCost - excessValue);
        var washedRate = outputKg > 0
            ? VoucherHelpers.Round2(washedCost / outputKg)
            : 0m;

        var sortOrder = 0;

        voucher.Entries.Add(new JournalEntry
        {
            AccountId = validation.UnwashedAccount.Id,
            VendorAccountId = validation.SourceVendor.Id,
            Description = $"Sent {inputKg:0.##} kg to washing room",
            Debit = 0,
            Credit = inputCost,
            Qty = null,
            ActualWeightKg = inputKg,
            Rbp = "Yes",
            Rate = sourceRate,
            SortOrder = ++sortOrder
        });

        var allocatedWashedDebit = 0m;
        for (var index = 0; index < validation.OutputLines.Count; index++)
        {
            var outputLine = validation.OutputLines[index];
            var lineDebit = index == validation.OutputLines.Count - 1
                ? VoucherHelpers.Round2(washedCost - allocatedWashedDebit)
                : VoucherHelpers.Round2(outputLine.WeightKg * washedRate);
            allocatedWashedDebit = VoucherHelpers.Round2(allocatedWashedDebit + lineDebit);

            voucher.Entries.Add(new JournalEntry
            {
                AccountId = outputLine.Account.Id,
                VendorAccountId = validation.SourceVendor.Id,
                Description = $"Received {outputLine.WeightKg:0.##} kg of {outputLine.Account.Name} from washing room @ {washedRate:0.##}/kg",
                Debit = lineDebit,
                Credit = 0,
                Qty = null,
                ActualWeightKg = outputLine.WeightKg,
                Rbp = "Yes",
                Rate = washedRate,
                SortOrder = ++sortOrder
            });
        }

        if (excessWastageKg > 0)
        {
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = validation.SourceVendor.Id,
                Description = $"Excess washing loss on {validation.UnwashedAccount.Name} ({excessWastageKg:0.##} kg over {validation.ThresholdPct:0.##}% threshold) charged to {validation.SourceVendor.Name} @ {sourceRate:0.##}/kg",
                Debit = excessValue,
                Credit = 0,
                Qty = null,
                ActualWeightKg = excessWastageKg,
                Rbp = null,
                Rate = sourceRate,
                SortOrder = ++sortOrder
            });
        }

        var totalDr = voucher.Entries.Sum(e => e.Debit);
        var totalCr = voucher.Entries.Sum(e => e.Credit);
        if (Math.Abs(totalDr - totalCr) > MassBalanceTolerance)
        {
            throw new RequestValidationException(
                $"GL balance failed. Dr {totalDr:0.##} vs Cr {totalCr:0.##}. Difference: {totalDr - totalCr:0.##}.");
        }

        _db.JournalVouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.WashingVoucher);

        if (voucher == null) return (false, null);

        voucher.IsDeleted = true;
        voucher.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<LatestUnwashedRateDto?> GetLatestUnwashedRateAsync(int vendorId, int unwashedAccountId)
    {
        if (vendorId <= 0 || unwashedAccountId <= 0) return null;

        var entry = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                e.SortOrder > 0 &&
                e.AccountId == unwashedAccountId &&
                e.Rate.HasValue && e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.PurchaseVoucher &&
                e.JournalVoucher.Entries.Any(p => p.SortOrder == 0 && p.AccountId == vendorId))
            .OrderByDescending(e => e.JournalVoucher.Date)
            .ThenByDescending(e => e.VoucherId)
            .ThenByDescending(e => e.Id)
            .Select(e => new LatestUnwashedRateDto
            {
                AccountId = e.AccountId,
                Rate = e.Rate!.Value,
                SourceVoucherNumber = e.JournalVoucher.VoucherNumber,
                SourceDate = e.JournalVoucher.Date
            })
            .FirstOrDefaultAsync();

        return entry;
    }

    private async Task<ValidatedWashingRequest> ValidateRequestAsync(CreateWashingVoucherRequest request)
    {
        if (request.SourceVendorId <= 0)
            throw new RequestValidationException("Pick the source vendor.");
        if (request.UnwashedAccountId <= 0)
            throw new RequestValidationException("Pick the unwashed material account.");
        if (request.InputWeightKg <= 0)
            throw new RequestValidationException("Input weight must be greater than zero.");

        var thresholdPct = request.ThresholdPct <= 0
            ? DefaultWastageThresholdPct
            : request.ThresholdPct;
        if (thresholdPct > 100m)
            throw new RequestValidationException("Threshold percentage cannot exceed 100.");

        var vendor = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.SourceVendorId);
        if (vendor == null || vendor.AccountType != AccountType.Party)
            throw new RequestValidationException("Source vendor must be a valid Party account.");

        var unwashed = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.UnwashedAccountId);
        if (unwashed == null || unwashed.AccountType != AccountType.UnwashedMaterial)
            throw new RequestValidationException("Unwashed account must be of type UnwashedMaterial.");

        var rateDto = await GetLatestUnwashedRateAsync(request.SourceVendorId, request.UnwashedAccountId);
        if (rateDto == null || rateDto.Rate <= 0)
            throw new RequestValidationException($"No purchase rate found for vendor '{vendor.Name}' on '{unwashed.Name}'. Record a purchase first.");

        var normalizedOutputs = await ResolveOutputLinesAsync(request, unwashed);
        var totalOutputWeight = VoucherHelpers.Round2(normalizedOutputs.Sum(line => line.WeightKg));
        if (totalOutputWeight <= 0)
            throw new RequestValidationException("Add at least one washed output line with weight greater than zero.");
        if (totalOutputWeight > request.InputWeightKg + MassBalanceTolerance)
            throw new RequestValidationException("Total output weight cannot exceed input weight.");

        return new ValidatedWashingRequest(vendor, unwashed, normalizedOutputs, rateDto.Rate, thresholdPct);
    }

    private WashingVoucherDto MapToDto(JournalVoucher voucher)
    {
        var entries = voucher.Entries.OrderBy(e => e.SortOrder).ToList();
        var unwashedEntry = entries.FirstOrDefault(e => e.Credit > 0 && e.Account?.AccountType == AccountType.UnwashedMaterial);
        var washedEntries = entries
            .Where(e => e.Debit > 0 && e.Account?.AccountType == AccountType.RawMaterial)
            .OrderBy(e => e.SortOrder)
            .ToList();
        var vendorEntry = entries.FirstOrDefault(e => e.Debit > 0 && e.Account?.AccountType == AccountType.Party);

        var inputKg = unwashedEntry?.ActualWeightKg ?? 0m;
        var outputKg = VoucherHelpers.Round2(washedEntries.Sum(e => e.ActualWeightKg ?? 0m));
        var wastageKg = VoucherHelpers.Round2(inputKg - outputKg);
        var wastagePct = inputKg > 0 ? VoucherHelpers.Round2((wastageKg / inputKg) * 100m) : 0m;
        var sourceRate = unwashedEntry?.Rate ?? 0m;
        var inputCost = VoucherHelpers.Round2(inputKg * sourceRate);
        var washedDebit = VoucherHelpers.Round2(washedEntries.Sum(e => e.Debit));
        var washedRate = outputKg > 0
            ? VoucherHelpers.Round2(washedDebit / outputKg)
            : washedEntries.FirstOrDefault()?.Rate ?? 0m;
        var excessKg = vendorEntry?.ActualWeightKg ?? 0m;
        var excessValue = vendorEntry?.Debit ?? 0m;
        var allowedKg = VoucherHelpers.Round2(wastageKg - excessKg);
        var thresholdPct = inputKg > 0 ? VoucherHelpers.Round2((allowedKg / inputKg) * 100m) : 0m;
        var outputLines = washedEntries.Select(entry => new WashingVoucherOutputLineDto
        {
            AccountId = entry.AccountId,
            AccountName = entry.Account?.Name ?? string.Empty,
            WeightKg = entry.ActualWeightKg ?? 0m,
            Rate = entry.Rate ?? washedRate,
            Debit = entry.Debit
        }).ToList();
        var singleOutput = outputLines.Count == 1 ? outputLines[0] : null;

        return new WashingVoucherDto
        {
            Id = voucher.Id,
            VoucherNumber = voucher.VoucherNumber,
            Date = voucher.Date,
            Description = voucher.Description,
            SourceVendorId = unwashedEntry?.VendorAccountId ?? vendorEntry?.AccountId ?? 0,
            SourceVendorName = vendorEntry?.Account?.Name ?? unwashedEntry?.VendorAccount?.Name,
            UnwashedAccountId = unwashedEntry?.AccountId ?? 0,
            UnwashedAccountName = unwashedEntry?.Account?.Name,
            WashedAccountId = singleOutput?.AccountId,
            WashedAccountName = singleOutput?.AccountName ?? (outputLines.Count > 1 ? $"Multiple outputs ({outputLines.Count})" : null),
            InputWeightKg = inputKg,
            OutputWeightKg = outputKg,
            OutputLines = outputLines,
            WastageKg = wastageKg,
            WastagePct = wastagePct,
            SourceRate = sourceRate,
            InputCost = inputCost,
            WashedDebit = washedDebit,
            WashedRate = washedRate,
            ThresholdPct = thresholdPct,
            ExcessWastageKg = excessKg,
            ExcessWastageValue = excessValue,
            CreatedAt = voucher.CreatedAt
        };
    }

    private WashingVoucherListDto MapToListDto(JournalVoucher voucher)
    {
        var entries = voucher.Entries.ToList();
        var unwashedEntry = entries.FirstOrDefault(e => e.Credit > 0 && e.Account?.AccountType == AccountType.UnwashedMaterial);
        var washedEntries = entries.Where(e => e.Debit > 0 && e.Account?.AccountType == AccountType.RawMaterial).ToList();
        var vendorEntry = entries.FirstOrDefault(e => e.Debit > 0 && e.Account?.AccountType == AccountType.Party);
        var inputKg = unwashedEntry?.ActualWeightKg ?? 0m;
        var outputKg = VoucherHelpers.Round2(washedEntries.Sum(e => e.ActualWeightKg ?? 0m));
        var wastageKg = VoucherHelpers.Round2(inputKg - outputKg);
        var wastagePct = inputKg > 0 ? VoucherHelpers.Round2((wastageKg / inputKg) * 100m) : 0m;
        var washedDebit = VoucherHelpers.Round2(washedEntries.Sum(e => e.Debit));

        return new WashingVoucherListDto
        {
            Id = voucher.Id,
            VoucherNumber = voucher.VoucherNumber,
            Date = voucher.Date,
            Description = voucher.Description,
            SourceVendorId = unwashedEntry?.VendorAccountId ?? 0,
            SourceVendorName = unwashedEntry?.VendorAccount?.Name,
            UnwashedAccountId = unwashedEntry?.AccountId ?? 0,
            UnwashedAccountName = unwashedEntry?.Account?.Name,
            InputWeightKg = inputKg,
            OutputWeightKg = outputKg,
            OutputLineCount = washedEntries.Count,
            WastageKg = wastageKg,
            WastagePct = wastagePct,
            WashedDebit = washedDebit,
            WashedRate = outputKg > 0
                ? VoucherHelpers.Round2(washedDebit / outputKg)
                : washedEntries.FirstOrDefault()?.Rate ?? 0m,
            ExcessWastageKg = vendorEntry?.ActualWeightKg ?? 0m,
            ExcessWastageValue = vendorEntry?.Debit ?? 0m,
            CreatedAt = voucher.CreatedAt
        };
    }

    private string BuildDescription(ValidatedWashingRequest validation)
    {
        if (validation.OutputLines.Count == 1)
        {
            return $"Washing: {validation.UnwashedAccount.Name} -> {validation.OutputLines[0].Account.Name}";
        }

        return $"Washing: {validation.UnwashedAccount.Name} -> {validation.OutputLines.Count} output molds";
    }

    private async Task<List<ValidatedWashingOutputLine>> ResolveOutputLinesAsync(CreateWashingVoucherRequest request, Account unwashed)
    {
        var requestedLines = request.OutputLines?
            .Where(line => line != null)
            .ToList() ?? [];

        if (requestedLines.Count == 0 && request.OutputWeightKg > 0 && unwashed.WashedAccountId.HasValue)
        {
            requestedLines.Add(new CreateWashingVoucherOutputLineRequest
            {
                AccountId = unwashed.WashedAccountId.Value,
                WeightKg = request.OutputWeightKg
            });
        }

        if (requestedLines.Count == 0)
        {
            return [];
        }

        var outputAccountIds = requestedLines
            .Select(line => line.AccountId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        var outputAccounts = await _db.Accounts
            .Where(a => outputAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        var normalized = new List<ValidatedWashingOutputLine>(requestedLines.Count);

        foreach (var line in requestedLines)
        {
            if (line.AccountId <= 0)
                throw new RequestValidationException("Each output line must have a washed raw material account.");

            var weightKg = VoucherHelpers.Round2(line.WeightKg);
            if (weightKg <= 0)
                throw new RequestValidationException("Each output line must have weight greater than zero.");

            if (!outputAccounts.TryGetValue(line.AccountId, out var account) || account.AccountType != AccountType.RawMaterial)
                throw new RequestValidationException("Each output line must reference an existing RawMaterial account.");

            normalized.Add(new ValidatedWashingOutputLine(account, weightKg));
        }

        return normalized;
    }

    private sealed record ValidatedWashingRequest(
        Account SourceVendor,
        Account UnwashedAccount,
        List<ValidatedWashingOutputLine> OutputLines,
        decimal SourceRate,
        decimal ThresholdPct);

    private sealed record ValidatedWashingOutputLine(
        Account Account,
        decimal WeightKg);

    private async Task<WashingVoucherDto?> GetVoucherAsync(System.Linq.Expressions.Expression<Func<JournalVoucher, bool>> predicate)
    {
        var voucher = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.WashingVoucher)
            .Where(predicate)
            .Include(j => j.Entries).ThenInclude(e => e.Account)
            .Include(j => j.Entries).ThenInclude(e => e.VendorAccount)
            .FirstOrDefaultAsync();

        return voucher == null ? null : MapToDto(voucher);
    }
}
