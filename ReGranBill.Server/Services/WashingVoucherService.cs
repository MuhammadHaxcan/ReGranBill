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
    private readonly IInventoryLotService _inventoryLotService;
    private readonly IDownstreamUsageService _downstreamUsageService;

    public WashingVoucherService(
        AppDbContext db,
        IVoucherNumberService voucherNumberService,
        IInventoryLotService inventoryLotService,
        IDownstreamUsageService downstreamUsageService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
        _inventoryLotService = inventoryLotService;
        _downstreamUsageService = downstreamUsageService;
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.WashingVoucher, "WSH-");

    public async Task<List<WashingVoucherListDto>> GetAllAsync()
    {
        var vouchers = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.WashingVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        var inputLots = await LoadInputLotsAsync(vouchers.Select(x => x.Id).ToArray());
        return vouchers.Select(v => MapToListDto(v, inputLots.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<WashingVoucherDto?> GetByIdAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.WashingVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync();
        if (voucher == null) return null;

        var inputLot = (await LoadInputLotsAsync([voucher.Id])).GetValueOrDefault(voucher.Id);
        return MapToDto(voucher, inputLot);
    }

    public async Task<WashingVoucherDto?> GetByNumberAsync(string voucherNumber)
    {
        var normalized = voucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        var voucher = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.WashingVoucher && j.VoucherNumber == normalized)
            .Include(j => j.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync();
        if (voucher == null) return null;

        var inputLot = (await LoadInputLotsAsync([voucher.Id])).GetValueOrDefault(voucher.Id);
        return MapToDto(voucher, inputLot);
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
            Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description) ?? BuildDescription(validation),
            RatesAdded = true,
            CreatedBy = userId
        };

        AppendJournalEntries(voucher, request, validation);
        _db.JournalVouchers.Add(voucher);
        await _db.SaveChangesAsync();

        await ApplyInventoryAsync(voucher, request, validation);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<WashingVoucherDto?> UpdateAsync(int id, CreateWashingVoucherRequest request)
    {
        var voucher = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.WashingVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;
        if (await HasDownstreamConsumptionAsync(id, VoucherType.WashingVoucher, InventoryTransactionType.WashOutput))
            throw new ConflictException("This washing voucher cannot be changed because one or more output lots have already been consumed.");

        var validation = await ValidateRequestAsync(request, id);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        voucher.Date = request.Date;
        voucher.Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description) ?? BuildDescription(validation);
        voucher.RatesAdded = true;
        voucher.UpdatedAt = DateTime.UtcNow;

        await RemoveVoucherInventoryAsync(id, VoucherType.WashingVoucher);
        await _db.SaveChangesAsync();

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();
        AppendJournalEntries(voucher, request, validation);
        await _db.SaveChangesAsync();

        await ApplyInventoryAsync(voucher, request, validation);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.WashingVoucher);

        if (voucher == null) return (false, null);

        if (await HasDownstreamConsumptionAsync(id, VoucherType.WashingVoucher, InventoryTransactionType.WashOutput))
            return (false, "Cannot delete a washing voucher after its output lots have been consumed.");

        voucher.IsDeleted = true;
        voucher.UpdatedAt = DateTime.UtcNow;
        await RemoveVoucherInventoryAsync(id, VoucherType.WashingVoucher);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<ValidatedWashingRequest> ValidateRequestAsync(CreateWashingVoucherRequest request, int? existingVoucherId = null)
    {
        if (request.SourceVendorId <= 0)
            throw new RequestValidationException("Pick the source vendor.");
        if (request.UnwashedAccountId <= 0)
            throw new RequestValidationException("Pick the unwashed material account.");
        if (request.SelectedLotId <= 0)
            throw new RequestValidationException("Pick the source lot.");
        if (request.InputWeightKg <= 0)
            throw new RequestValidationException("Input weight must be greater than zero.");

        var thresholdPct = request.ThresholdPct <= 0 ? DefaultWastageThresholdPct : request.ThresholdPct;
        if (thresholdPct > 100m)
            throw new RequestValidationException("Threshold percentage cannot exceed 100.");

        var vendor = await _db.Accounts
            .Include(x => x.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == request.SourceVendorId);
        if (vendor == null || vendor.AccountType != AccountType.Party)
            throw new RequestValidationException("Source vendor must be a valid Party account.");

        var unwashed = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == request.UnwashedAccountId);
        if (unwashed == null || unwashed.AccountType != AccountType.UnwashedMaterial)
            throw new RequestValidationException("Unwashed account must be of type UnwashedMaterial.");

        var selectedLot = await _db.InventoryLots
            .Include(x => x.VendorAccount)
            .Include(x => x.ProductAccount)
            .FirstOrDefaultAsync(x => x.Id == request.SelectedLotId && x.Status == InventoryLotStatus.Open);
        if (selectedLot == null)
            throw new RequestValidationException("Selected lot does not exist.");
        if (selectedLot.ProductAccountId != request.UnwashedAccountId)
            throw new RequestValidationException("Selected lot does not belong to the chosen unwashed material.");
        if (selectedLot.VendorAccountId != request.SourceVendorId)
            throw new RequestValidationException("Selected lot does not belong to the chosen vendor.");

        var available = (await _inventoryLotService.GetAvailableWeightByLotIdsAsync([selectedLot.Id]))
            .GetValueOrDefault(selectedLot.Id);
        if (existingVoucherId.HasValue)
        {
            var currentVoucherKg = await _db.InventoryTransactions
                .Where(x => x.VoucherId == existingVoucherId.Value
                    && x.VoucherType == VoucherType.WashingVoucher
                    && x.TransactionType == InventoryTransactionType.WashConsume
                    && x.LotId == selectedLot.Id)
                .SumAsync(x => (decimal?)(x.WeightKgDelta < 0 ? -x.WeightKgDelta : x.WeightKgDelta)) ?? 0m;
            available = VoucherHelpers.Round2(available + currentVoucherKg);
        }
        if (request.InputWeightKg - available > MassBalanceTolerance)
            throw new RequestValidationException($"Input weight cannot exceed available lot balance of {available:0.##} kg.");

        var normalizedOutputs = await ResolveOutputLinesAsync(request);
        var totalOutputWeight = VoucherHelpers.Round2(normalizedOutputs.Sum(line => line.WeightKg));
        if (totalOutputWeight <= 0)
            throw new RequestValidationException("Add at least one washed output line with weight greater than zero.");
        if (totalOutputWeight > request.InputWeightKg + MassBalanceTolerance)
            throw new RequestValidationException("Total output weight cannot exceed input weight.");

        var inputRate = request.InputRate > 0 ? request.InputRate : selectedLot.BaseRate;
        if (inputRate <= 0)
            throw new RequestValidationException("Input rate must be greater than zero.");

        return new ValidatedWashingRequest(vendor, unwashed, selectedLot, normalizedOutputs, inputRate, thresholdPct);
    }

    private async Task<List<ValidatedWashingOutputLine>> ResolveOutputLinesAsync(CreateWashingVoucherRequest request)
    {
        var requestedLines = request.OutputLines?.Where(line => line != null).ToList() ?? [];
        if (requestedLines.Count == 0)
            return [];

        var outputAccountIds = requestedLines.Select(line => line.AccountId).Where(id => id > 0).Distinct().ToArray();
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

    private static void AppendJournalEntries(JournalVoucher voucher, CreateWashingVoucherRequest request, ValidatedWashingRequest validation)
    {
        var thresholdRatio = validation.ThresholdPct / 100m;
        var inputKg = VoucherHelpers.Round2(request.InputWeightKg);
        var outputKg = VoucherHelpers.Round2(validation.OutputLines.Sum(line => line.WeightKg));
        var wastageKg = VoucherHelpers.Round2(inputKg - outputKg);
        var inputCost = VoucherHelpers.Round2(inputKg * validation.InputRate);
        var excessWastageKg = VoucherHelpers.Round2(Math.Max(0m, wastageKg - thresholdRatio * inputKg));
        var excessValue = VoucherHelpers.Round2(excessWastageKg * validation.InputRate);
        var washedCost = VoucherHelpers.Round2(inputCost - excessValue);
        var washedRate = outputKg > 0 ? VoucherHelpers.Round2(washedCost / outputKg) : 0m;

        var sortOrder = 0;
        voucher.Entries.Add(new JournalEntry
        {
            AccountId = validation.UnwashedAccount.Id,
            Description = $"Sent {inputKg:0.##} kg from lot {validation.SelectedLot.LotNumber} to washing room",
            Debit = 0,
            Credit = inputCost,
            ActualWeightKg = inputKg,
            Rbp = "Yes",
            Rate = validation.InputRate,
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
                Description = $"Received {outputLine.WeightKg:0.##} kg of {outputLine.Account.Name} from washing room",
                Debit = lineDebit,
                Credit = 0,
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
                Description = $"Excess washing loss charged to {validation.SourceVendor.Name}",
                Debit = excessValue,
                Credit = 0,
                ActualWeightKg = excessWastageKg,
                Rate = validation.InputRate,
                SortOrder = ++sortOrder
            });
        }
    }

    private async Task ApplyInventoryAsync(JournalVoucher voucher, CreateWashingVoucherRequest request, ValidatedWashingRequest validation)
    {
        var inputEntry = voucher.Entries.OrderBy(x => x.SortOrder).First(x => x.Credit > 0 && x.AccountId == validation.UnwashedAccount.Id);
        var consumeTransaction = new InventoryTransaction
        {
            VoucherId = voucher.Id,
            VoucherType = VoucherType.WashingVoucher,
            VoucherLineKey = "washing-input",
            TransactionType = InventoryTransactionType.WashConsume,
            ProductAccountId = validation.SelectedLot.ProductAccountId,
            LotId = validation.SelectedLot.Id,
            QtyDelta = null,
            WeightKgDelta = -VoucherHelpers.Round2(request.InputWeightKg),
            Rate = validation.InputRate,
            ValueDelta = -VoucherHelpers.Round2(request.InputWeightKg * validation.InputRate),
            TransactionDate = voucher.Date,
            Notes = inputEntry.Description
        };
        _db.InventoryTransactions.Add(consumeTransaction);
        await _db.SaveChangesAsync();

        _db.InventoryVoucherLinks.Add(new InventoryVoucherLink
        {
            VoucherId = voucher.Id,
            VoucherType = VoucherType.WashingVoucher,
            VoucherLineKey = "washing-input",
            LotId = validation.SelectedLot.Id,
            TransactionId = consumeTransaction.Id
        });

        var outputEntries = voucher.Entries
            .Where(x => x.Debit > 0 && x.Account.AccountType == AccountType.RawMaterial)
            .OrderBy(x => x.SortOrder)
            .ToList();

        for (var index = 0; index < outputEntries.Count; index++)
        {
            var entry = outputEntries[index];
            var lot = new InventoryLot
            {
                LotNumber = $"{voucher.VoucherNumber}-O{index + 1:00}",
                ProductAccountId = entry.AccountId,
                VendorAccountId = validation.SelectedLot.VendorAccountId,
                SourceVoucherId = voucher.Id,
                SourceVoucherType = VoucherType.WashingVoucher,
                SourceEntryId = entry.Id,
                ParentLotId = validation.SelectedLot.Id,
                OriginalQty = null,
                OriginalWeightKg = entry.ActualWeightKg ?? 0m,
                BaseRate = entry.Rate ?? 0m,
                Status = InventoryLotStatus.Open
            };
            _db.InventoryLots.Add(lot);
            await _db.SaveChangesAsync();

            var outputTransaction = new InventoryTransaction
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.WashingVoucher,
                VoucherLineKey = $"washing-output-{index + 1}",
                TransactionType = InventoryTransactionType.WashOutput,
                ProductAccountId = entry.AccountId,
                LotId = lot.Id,
                QtyDelta = null,
                WeightKgDelta = entry.ActualWeightKg ?? 0m,
                Rate = entry.Rate ?? 0m,
                ValueDelta = entry.Debit,
                TransactionDate = voucher.Date,
                Notes = entry.Description
            };
            _db.InventoryTransactions.Add(outputTransaction);
            await _db.SaveChangesAsync();

            _db.InventoryVoucherLinks.Add(new InventoryVoucherLink
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.WashingVoucher,
                VoucherLineKey = $"washing-output-{index + 1}",
                LotId = lot.Id,
                TransactionId = outputTransaction.Id
            });
        }
    }

    private async Task<Dictionary<int, InventoryLot?>> LoadInputLotsAsync(IReadOnlyCollection<int> voucherIds)
    {
        var ids = voucherIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var rows = await _db.InventoryVoucherLinks
            .AsNoTracking()
            .Where(x => ids.Contains(x.VoucherId) && x.VoucherType == VoucherType.WashingVoucher && x.VoucherLineKey == "washing-input")
            .Select(x => new
            {
                x.VoucherId,
                Lot = x.Lot
            })
            .ToListAsync();

        var lotIds = rows.Select(x => x.Lot.Id).Distinct().ToArray();
        var lots = await _db.InventoryLots
            .AsNoTracking()
            .Include(x => x.ProductAccount)
            .Include(x => x.VendorAccount)
            .Where(x => lotIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return rows.ToDictionary(x => x.VoucherId, x => lots.GetValueOrDefault(x.Lot.Id));
    }

    private async Task RemoveVoucherInventoryAsync(int voucherId, VoucherType voucherType)
    {
        var links = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == voucherType)
            .ToListAsync();
        if (links.Count == 0)
            return;

        var transactionIds = links.Select(x => x.TransactionId).Distinct().ToList();
        var createdLotIds = await _db.InventoryTransactions
            .Where(x => transactionIds.Contains(x.Id) && x.WeightKgDelta > 0)
            .Select(x => x.LotId)
            .Distinct()
            .ToListAsync();

        var transactions = await _db.InventoryTransactions.Where(x => transactionIds.Contains(x.Id)).ToListAsync();
        var createdLots = await _db.InventoryLots.Where(x => createdLotIds.Contains(x.Id)).ToListAsync();

        _db.InventoryVoucherLinks.RemoveRange(links);
        _db.InventoryTransactions.RemoveRange(transactions);
        foreach (var lot in createdLots)
        {
            lot.Status = InventoryLotStatus.Voided;
            lot.SourceEntryId = null;
            lot.UpdatedAt = DateTime.UtcNow;
        }
    }

    private Task<bool> HasDownstreamConsumptionAsync(int voucherId, VoucherType voucherType, InventoryTransactionType outputType) =>
        _downstreamUsageService.HasAnyForWashingAsync(voucherId);

    private static WashingVoucherDto MapToDto(JournalVoucher voucher, InventoryLot? inputLot)
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
        var sourceRate = unwashedEntry?.Rate ?? inputLot?.BaseRate ?? 0m;
        var inputCost = VoucherHelpers.Round2(inputKg * sourceRate);
        var washedDebit = VoucherHelpers.Round2(washedEntries.Sum(e => e.Debit));
        var washedRate = outputKg > 0 ? VoucherHelpers.Round2(washedDebit / outputKg) : washedEntries.FirstOrDefault()?.Rate ?? 0m;
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
            SourceVendorId = inputLot?.VendorAccountId ?? 0,
            SourceVendorName = inputLot?.VendorAccount?.Name,
            UnwashedAccountId = unwashedEntry?.AccountId ?? inputLot?.ProductAccountId ?? 0,
            UnwashedAccountName = unwashedEntry?.Account?.Name ?? inputLot?.ProductAccount?.Name,
            SelectedLotId = inputLot?.Id ?? 0,
            SelectedLotNumber = inputLot?.LotNumber,
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

    private static WashingVoucherListDto MapToListDto(JournalVoucher voucher, InventoryLot? inputLot)
    {
        var dto = MapToDto(voucher, inputLot);
        return new WashingVoucherListDto
        {
            Id = dto.Id,
            VoucherNumber = dto.VoucherNumber,
            Date = dto.Date,
            Description = dto.Description,
            SourceVendorId = dto.SourceVendorId,
            SourceVendorName = dto.SourceVendorName,
            UnwashedAccountId = dto.UnwashedAccountId,
            UnwashedAccountName = dto.UnwashedAccountName,
            SelectedLotId = dto.SelectedLotId,
            SelectedLotNumber = dto.SelectedLotNumber,
            InputWeightKg = dto.InputWeightKg,
            OutputWeightKg = dto.OutputWeightKg,
            OutputLineCount = dto.OutputLines.Count,
            WastageKg = dto.WastageKg,
            WastagePct = dto.WastagePct,
            WashedDebit = dto.WashedDebit,
            WashedRate = dto.WashedRate,
            ExcessWastageKg = dto.ExcessWastageKg,
            ExcessWastageValue = dto.ExcessWastageValue,
            CreatedAt = dto.CreatedAt
        };
    }

    private static string BuildDescription(ValidatedWashingRequest validation)
    {
        if (validation.OutputLines.Count == 1)
            return $"Washing: {validation.UnwashedAccount.Name} -> {validation.OutputLines[0].Account.Name}";

        return $"Washing: {validation.UnwashedAccount.Name} -> {validation.OutputLines.Count} output molds";
    }

    private sealed record ValidatedWashingRequest(
        Account SourceVendor,
        Account UnwashedAccount,
        InventoryLot SelectedLot,
        List<ValidatedWashingOutputLine> OutputLines,
        decimal InputRate,
        decimal ThresholdPct);

    private sealed record ValidatedWashingOutputLine(Account Account, decimal WeightKg);
}
