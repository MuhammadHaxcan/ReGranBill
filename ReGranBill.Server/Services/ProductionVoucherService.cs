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
    private readonly IInventoryLotService _inventoryLotService;
    private readonly IDownstreamUsageService _downstreamUsageService;

    public ProductionVoucherService(
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
            .FirstOrDefaultAsync();

        if (voucher == null) return null;
        var selectedLots = await LoadSelectedLotsAsync(voucher.Id);
        return MapToDto(voucher, selectedLots);
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

        await ApplyInventoryAsync(voucher, request, validation);
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
        if (await HasDownstreamConsumptionAsync(id))
            throw new ConflictException("This production voucher cannot be changed because one or more output lots have already been consumed.");

        var validation = await ValidateRequestAsync(request, id);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        voucher.Date = request.Date;
        voucher.VehicleNumber = VoucherHelpers.ToNullIfWhiteSpace(request.LotNumber);
        voucher.Description = BuildDescription(request, validation);
        voucher.UpdatedAt = DateTime.UtcNow;

        await RemoveVoucherInventoryAsync(id);
        await _db.SaveChangesAsync();

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();
        AppendEntries(voucher, request, validation, isEdited: true);
        await _db.SaveChangesAsync();

        await ApplyInventoryAsync(voucher, request, validation);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.ProductionVoucher);

        if (voucher == null) return (false, null);
        if (await HasDownstreamConsumptionAsync(id))
            return (false, "Cannot delete a production voucher after its output lots have been consumed.");

        voucher.IsDeleted = true;
        voucher.UpdatedAt = DateTime.UtcNow;
        await RemoveVoucherInventoryAsync(id);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<ValidatedRequest> ValidateRequestAsync(CreateProductionVoucherRequest request, int? existingVoucherId = null)
    {
        if (request.Inputs.Count == 0)
            throw new RequestValidationException("Add at least one input line.");
        if (request.Outputs.Count == 0)
            throw new RequestValidationException("Add at least one output line.");

        ValidateLines(request.Inputs, "input", requireSelectedLot: true, requireRate: true);
        ValidateLines(request.Outputs, "output", requireSelectedLot: false, requireRate: false);
        ValidateLines(request.Byproducts, "byproduct", requireSelectedLot: false, requireRate: false);

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
            .Concat(request.Shortage != null && request.Shortage.WeightKg > 0 ? [request.Shortage.AccountId] : [])
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

        foreach (var line in request.Outputs)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account)
                || account.AccountType != AccountType.Product)
            {
                throw new RequestValidationException("Each output must be a valid Product account.");
            }
        }

        foreach (var line in request.Byproducts)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account)
                || (account.AccountType != AccountType.RawMaterial && account.AccountType != AccountType.Product))
            {
                throw new RequestValidationException("Each byproduct must be a valid Raw Material or Product account.");
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

        var selectedLotIds = request.Inputs
            .Where(l => l.SelectedLotId.HasValue && l.SelectedLotId.Value > 0)
            .Select(l => l.SelectedLotId!.Value)
            .Distinct()
            .ToList();
        var selectedLots = await _db.InventoryLots
            .Include(x => x.ProductAccount)
            .Include(x => x.VendorAccount)
            .Where(x => selectedLotIds.Contains(x.Id) && x.Status == InventoryLotStatus.Open)
            .ToDictionaryAsync(x => x.Id);

        var availableByLot = await _inventoryLotService.GetAvailableWeightByLotIdsAsync(selectedLotIds);
        if (existingVoucherId.HasValue)
        {
            var currentVoucherLoads = await _db.InventoryTransactions
                .Where(x => x.VoucherId == existingVoucherId.Value
                    && x.VoucherType == VoucherType.ProductionVoucher
                    && x.TransactionType == InventoryTransactionType.ProductionConsume)
                .GroupBy(x => x.LotId)
                .Select(g => new { g.Key, Kg = Math.Abs(g.Sum(x => x.WeightKgDelta)) })
                .ToListAsync();

            foreach (var row in currentVoucherLoads)
            {
                availableByLot[row.Key] = availableByLot.GetValueOrDefault(row.Key) + row.Kg;
            }
        }

        var requestedByLot = request.Inputs
            .GroupBy(x => x.SelectedLotId!.Value)
            .ToDictionary(g => g.Key, g => VoucherHelpers.Round2(g.Sum(x => x.WeightKg)));

        foreach (var line in request.Inputs)
        {
            var lotId = line.SelectedLotId!.Value;
            if (!selectedLots.TryGetValue(lotId, out var lot))
                throw new RequestValidationException("Each input line must select a valid lot.");
            if (lot.ProductAccountId != line.AccountId)
                throw new RequestValidationException("Selected lot does not match the chosen input material.");
        }

        foreach (var pair in requestedByLot)
        {
            var available = VoucherHelpers.Round2(availableByLot.GetValueOrDefault(pair.Key));
            if (pair.Value - available > MassBalanceTolerance)
                throw new RequestValidationException($"Requested input exceeds available lot balance of {available:0.##} kg.");
        }

        var totalInputCost = request.Inputs.Sum(l => VoucherHelpers.Round2(l.WeightKg) * l.Rate!.Value);
        var totalPhysicalOutKg = VoucherHelpers.Round2(totalOutputKg + totalByproductKg + shortageKg);
        var derivedRate = totalPhysicalOutKg > 0
            ? VoucherHelpers.Round2(totalInputCost / totalPhysicalOutKg)
            : 0m;

        return new ValidatedRequest(accounts, selectedLots, derivedRate, VoucherHelpers.Round2(totalInputCost));
    }

    private static void AppendEntries(JournalVoucher voucher, CreateProductionVoucherRequest request, ValidatedRequest validation, bool isEdited)
    {
        var sortOrder = 0;

        foreach (var line in request.Inputs.OrderBy(l => l.SortOrder))
        {
            var account = validation.Accounts[line.AccountId];
            var lot = validation.SelectedLots[line.SelectedLotId!.Value];
            var weight = VoucherHelpers.Round2(line.WeightKg);
            var rate = line.Rate!.Value;
            var amount = VoucherHelpers.Round2(weight * rate);
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description)
                    ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg from lot {lot.LotNumber}",
                Debit = 0,
                Credit = amount,
                Qty = line.Qty,
                ActualWeightKg = weight,
                Rbp = "Yes",
                Rate = rate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Input,
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
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description) ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg",
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
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description) ?? $"{account.Name} - {line.Qty} bags / {weight:0.##} kg",
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
                ActualWeightKg = shortageKg,
                Rbp = "Yes",
                Rate = derivedRate,
                IsEdited = isEdited,
                LineKind = ProductionLineKind.Shortage,
                SortOrder = ++sortOrder
            });
        }
    }

    private async Task ApplyInventoryAsync(JournalVoucher voucher, CreateProductionVoucherRequest request, ValidatedRequest validation)
    {
        var inputEntries = voucher.Entries.Where(x => x.LineKind == ProductionLineKind.Input).OrderBy(x => x.SortOrder).ToList();
        for (var index = 0; index < inputEntries.Count; index++)
        {
            var entry = inputEntries[index];
            var input = request.Inputs.OrderBy(x => x.SortOrder).ElementAt(index);
            var lot = validation.SelectedLots[input.SelectedLotId!.Value];

            var tx = new InventoryTransaction
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.ProductionVoucher,
                VoucherLineKey = $"production-input-{entry.SortOrder}",
                TransactionType = InventoryTransactionType.ProductionConsume,
                ProductAccountId = entry.AccountId,
                LotId = lot.Id,
                QtyDelta = entry.Qty > 0 ? -entry.Qty : null,
                WeightKgDelta = -(entry.ActualWeightKg ?? 0m),
                Rate = entry.Rate ?? 0m,
                ValueDelta = -entry.Credit,
                TransactionDate = voucher.Date,
                Notes = entry.Description
            };
            _db.InventoryTransactions.Add(tx);
            await _db.SaveChangesAsync();

            _db.InventoryVoucherLinks.Add(new InventoryVoucherLink
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.ProductionVoucher,
                VoucherLineKey = $"production-input-{entry.SortOrder}",
                LotId = lot.Id,
                TransactionId = tx.Id
            });
        }

        var uniqueVendorIds = validation.SelectedLots.Values
            .Select(x => x.VendorAccountId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var outputVendorId = uniqueVendorIds.Count == 1 ? uniqueVendorIds[0] : (int?)null;

        var outputEntries = voucher.Entries
            .Where(x => x.LineKind == ProductionLineKind.Output || x.LineKind == ProductionLineKind.Byproduct)
            .OrderBy(x => x.SortOrder)
            .ToList();

        foreach (var entry in outputEntries)
        {
            var prefix = entry.LineKind == ProductionLineKind.Output ? "O" : "B";
            var ordinal = outputEntries.Count(x => x.SortOrder <= entry.SortOrder && x.LineKind == entry.LineKind);
            var lineKey = entry.LineKind == ProductionLineKind.Output
                ? $"production-output-{ordinal}"
                : $"production-byproduct-{ordinal}";

            var lot = new InventoryLot
            {
                LotNumber = $"{voucher.VoucherNumber}-{prefix}{ordinal:00}",
                ProductAccountId = entry.AccountId,
                VendorAccountId = outputVendorId,
                SourceVoucherId = voucher.Id,
                SourceVoucherType = VoucherType.ProductionVoucher,
                SourceEntryId = entry.Id,
                ParentLotId = null,
                OriginalQty = entry.Qty > 0 ? entry.Qty : null,
                OriginalWeightKg = entry.ActualWeightKg ?? 0m,
                BaseRate = entry.Rate ?? 0m,
                Status = InventoryLotStatus.Open
            };
            _db.InventoryLots.Add(lot);
            await _db.SaveChangesAsync();

            var tx = new InventoryTransaction
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.ProductionVoucher,
                VoucherLineKey = lineKey,
                TransactionType = InventoryTransactionType.ProductionOutput,
                ProductAccountId = entry.AccountId,
                LotId = lot.Id,
                QtyDelta = entry.Qty > 0 ? entry.Qty : null,
                WeightKgDelta = entry.ActualWeightKg ?? 0m,
                Rate = entry.Rate ?? 0m,
                ValueDelta = entry.Debit,
                TransactionDate = voucher.Date,
                Notes = entry.Description
            };
            _db.InventoryTransactions.Add(tx);
            await _db.SaveChangesAsync();

            _db.InventoryVoucherLinks.Add(new InventoryVoucherLink
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.ProductionVoucher,
                VoucherLineKey = lineKey,
                LotId = lot.Id,
                TransactionId = tx.Id
            });
        }
    }

    private async Task<Dictionary<string, InventoryLot>> LoadSelectedLotsAsync(int voucherId)
    {
        var links = await _db.InventoryVoucherLinks
            .AsNoTracking()
            .Where(x => x.VoucherId == voucherId && x.VoucherType == VoucherType.ProductionVoucher && x.VoucherLineKey.StartsWith("production-input-"))
            .ToListAsync();
        if (links.Count == 0)
            return [];

        var lots = await _db.InventoryLots
            .AsNoTracking()
            .Include(x => x.VendorAccount)
            .Where(x => links.Select(l => l.LotId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return links.ToDictionary(x => x.VoucherLineKey, x => lots[x.LotId]);
    }

    private Task<bool> HasDownstreamConsumptionAsync(int voucherId) =>
        _downstreamUsageService.HasAnyForProductionAsync(voucherId);

    private async Task RemoveVoucherInventoryAsync(int voucherId)
    {
        var links = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == VoucherType.ProductionVoucher)
            .ToListAsync();
        if (links.Count == 0)
            return;

        var transactionIds = links.Select(x => x.TransactionId).Distinct().ToList();
        var createdLotIds = await _db.InventoryTransactions
            .Where(x => transactionIds.Contains(x.Id) && x.TransactionType == InventoryTransactionType.ProductionOutput)
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

    private static string? BuildDescription(CreateProductionVoucherRequest request, ValidatedRequest validation)
    {
        var trimmed = VoucherHelpers.ToNullIfWhiteSpace(request.Description);
        if (trimmed != null) return trimmed;

        var inputNames = string.Join(", ", request.Inputs.OrderBy(l => l.SortOrder).Select(l => validation.Accounts[l.AccountId].Name));
        var outputNames = string.Join(", ", request.Outputs.OrderBy(l => l.SortOrder).Select(l => validation.Accounts[l.AccountId].Name));
        if (string.IsNullOrEmpty(inputNames) && string.IsNullOrEmpty(outputNames)) return null;
        return $"Production: {inputNames} -> {outputNames}";
    }

    private static void ValidateLines(IList<ProductionLineRequest> lines, string label, bool requireSelectedLot, bool requireRate)
    {
        foreach (var line in lines)
        {
            if (line.AccountId <= 0)
                throw new RequestValidationException($"Each {label} line must reference a valid account.");
            if (line.Qty < 0)
                throw new RequestValidationException($"{label} bags cannot be negative.");
            if (line.WeightKg <= 0)
                throw new RequestValidationException($"{label} weight must be greater than zero.");
            if (requireSelectedLot && (!line.SelectedLotId.HasValue || line.SelectedLotId.Value <= 0))
                throw new RequestValidationException($"Each {label} line must select a lot.");
            if (requireRate && (!line.Rate.HasValue || line.Rate.Value <= 0))
                throw new RequestValidationException($"Each {label} line must have a rate greater than zero.");
        }
    }

    private static decimal SumKg(IEnumerable<JournalEntry> entries, ProductionLineKind kind) =>
        VoucherHelpers.Round2(entries.Where(e => e.LineKind == kind).Sum(e => e.ActualWeightKg ?? 0m));

    private static ProductionVoucherDto MapToDto(JournalVoucher voucher, IReadOnlyDictionary<string, InventoryLot> selectedLots)
    {
        var entries = voucher.Entries.OrderBy(e => e.SortOrder).ToList();

        ProductionLineDto LineDto(JournalEntry e) => new()
        {
            Id = e.Id,
            AccountId = e.AccountId,
            SelectedLotId = e.LineKind == ProductionLineKind.Input ? selectedLots.GetValueOrDefault($"production-input-{e.SortOrder}")?.Id : null,
            SelectedLotNumber = e.LineKind == ProductionLineKind.Input ? selectedLots.GetValueOrDefault($"production-input-{e.SortOrder}")?.LotNumber : null,
            AccountName = e.Account?.Name,
            Packing = e.Account?.ProductDetail?.Packing,
            PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg,
            Qty = e.Qty ?? 0,
            WeightKg = e.ActualWeightKg ?? 0,
            Description = e.Description,
            SortOrder = e.SortOrder,
            VendorId = e.LineKind == ProductionLineKind.Input ? selectedLots.GetValueOrDefault($"production-input-{e.SortOrder}")?.VendorAccountId : null,
            VendorName = e.LineKind == ProductionLineKind.Input ? selectedLots.GetValueOrDefault($"production-input-{e.SortOrder}")?.VendorAccount?.Name : null,
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
        IReadOnlyDictionary<int, InventoryLot> SelectedLots,
        decimal DerivedRate,
        decimal TotalInputCost);
}
