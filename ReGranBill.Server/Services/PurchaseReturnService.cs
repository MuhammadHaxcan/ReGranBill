using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.PurchaseReturns;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class PurchaseReturnService : IPurchaseReturnService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;
    private readonly IInventoryLotService _inventoryLotService;

    public PurchaseReturnService(AppDbContext db, IVoucherNumberService voucherNumberService, IInventoryLotService inventoryLotService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
        _inventoryLotService = inventoryLotService;
    }

    public async Task<List<PurchaseReturnDto>> GetAllAsync()
    {
        var prJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        var selectedLots = await LoadSelectedLotsAsync(prJvs.Select(x => x.Id).ToArray());
        return prJvs.Select(v => MapToDto(v, selectedLots.GetValueOrDefault(v.Id))).ToList();
    }

    public async Task<PurchaseReturnDto?> GetByIdAsync(int id)
    {
        return await GetVoucherAsync(j => j.Id == id);
    }

    public async Task<PurchaseReturnDto?> GetByNumberAsync(string prNumber)
    {
        var normalized = prNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return await GetVoucherAsync(j => j.VoucherNumber == normalized);
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.PurchaseReturn, "PR-");

    public async Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds)
    {
        var ids = productIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return [];

        var entries = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                e.SortOrder > 0 &&
                ids.Contains(e.AccountId) &&
                e.Rate.HasValue &&
                e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.PurchaseVoucher)
            .Select(e => new
            {
                e.AccountId,
                Rate = e.Rate!.Value,
                e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherDate = e.JournalVoucher.Date
            })
            .OrderByDescending(e => e.VoucherDate)
            .ThenByDescending(e => e.VoucherId)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        return entries
            .GroupBy(e => e.AccountId)
            .Select(group => group.First())
            .Select(item => new LatestProductRateDto
            {
                ProductId = item.AccountId,
                Rate = item.Rate,
                SourceVoucherNumber = item.VoucherNumber,
                SourceDate = item.VoucherDate
            })
            .ToList();
    }

    public async Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request);
        var prDate = request.Date;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var prNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.PurchaseReturn, "PR-");

        var prJv = new JournalVoucher
        {
            VoucherNumber = prNumber,
            Date = prDate,
            VoucherType = VoucherType.PurchaseReturnVoucher,
            VehicleNumber = request.VehicleNumber,
            Description = VoucherHelpers.ResolveDescription(request.Description, "Return to", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts),
            RatesAdded = request.Lines.All(l => l.Rate > 0),
            CreatedBy = userId,
        };

        var totalProductAmount = 0m;

        // SortOrder 0: Vendor DEBIT (we return goods, reduce what we owe)
        prJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Return to vendor - {prNumber}",
            Debit = 0,
            Credit = 0,
            IsEdited = false,
            SortOrder = 0
        });

        var sortOrder = 0;
        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = CalculateLineAmt(line.TotalWeightKg, line.Rate);
            totalProductAmount += lineAmount;

            prJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags / {line.TotalWeightKg:0.##} kg",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                ActualWeightKg = line.TotalWeightKg,
                Rbp = "Yes",
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        // Set vendor debit equal to total inventory credits
        var vendorEntry = prJv.Entries.First(e => e.SortOrder == 0);
        vendorEntry.Debit = totalProductAmount;

        _db.JournalVouchers.Add(prJv);
        await _db.SaveChangesAsync();
        await RebuildInventoryAsync(prJv, request, validation);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(prJv.Id))!;
    }

    public async Task<PurchaseReturnDto?> UpdateAsync(int id, CreatePurchaseReturnRequest request)
    {
        var prJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (prJv == null) return null;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        await EnsureExistingSourceLotsUsableAsync(prJv.Id);
        var validation = await ValidateRequestAsync(request, id);

        prJv.Date = request.Date;
        prJv.VehicleNumber = request.VehicleNumber;
        prJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Return to", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        prJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        prJv.UpdatedAt = DateTime.UtcNow;

        await RemoveVoucherInventoryAsync(prJv.Id);
        _db.JournalEntries.RemoveRange(prJv.Entries);
        prJv.Entries.Clear();

        var totalProductAmount = 0m;

        prJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Return to vendor - {prJv.VoucherNumber}",
            Debit = 0,
            Credit = 0,
            IsEdited = true,
            SortOrder = 0
        });

        var sortOrder = 0;
        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = CalculateLineAmt(line.TotalWeightKg, line.Rate);
            totalProductAmount += lineAmount;

            prJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags / {line.TotalWeightKg:0.##} kg",
                Debit = 0,
                Credit = lineAmount,
                Qty = line.Qty,
                ActualWeightKg = line.TotalWeightKg,
                Rbp = "Yes",
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        var vendorEntry = prJv.Entries.First(e => e.SortOrder == 0);
        vendorEntry.Debit = totalProductAmount;

        await _db.SaveChangesAsync();
        await RebuildInventoryAsync(prJv, request, validation);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(prJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdatePurchaseReturnRatesRequest request)
    {
        var prJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .FirstOrDefaultAsync();

        if (prJv == null) return false;

        foreach (var update in request.Lines)
        {
            var entry = prJv.Entries.FirstOrDefault(e => e.Id == update.EntryId);
            if (entry != null && entry.SortOrder > 0)
            {
                entry.Rate = update.Rate;
                entry.IsEdited = true;
                entry.Credit = CalculateLineAmt(entry.ActualWeightKg ?? 0m, update.Rate);
            }
        }

        var vendorEntry = prJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        if (vendorEntry != null)
        {
            vendorEntry.Debit = prJv.Entries.Where(e => e.SortOrder > 0).Sum(e => e.Credit);
            vendorEntry.IsEdited = true;
        }

        prJv.RatesAdded = prJv.Entries.Where(e => e.SortOrder > 0).All(e => e.Rate > 0);
        prJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        var updateRequest = await BuildUpdateRequestAsync(prJv);
        var validation = await ValidateRequestAsync(updateRequest, id);
        await RebuildInventoryAsync(prJv, updateRequest, validation);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var prJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.PurchaseReturnVoucher);

        if (prJv == null) return (false, null);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var voidedLotNumbers = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == prJv.Id && x.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Join(_db.InventoryLots, link => link.LotId, lot => lot.Id, (link, lot) => lot)
            .Where(lot => lot.Status != InventoryLotStatus.Open)
            .Select(lot => lot.LotNumber)
            .Distinct()
            .ToListAsync();

        if (voidedLotNumbers.Count > 0)
        {
            return (false, $"Cannot delete this purchase return because its source lot(s) are not open: {string.Join(", ", voidedLotNumbers)}.");
        }

        prJv.IsDeleted = true;
        prJv.UpdatedAt = DateTime.UtcNow;

        await RemoveVoucherInventoryAsync(prJv.Id);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (true, null);
    }

    private async Task EnsureExistingSourceLotsUsableAsync(int prVoucherId)
    {
        var problematicLotNumbers = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == prVoucherId && x.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Join(_db.InventoryLots, link => link.LotId, lot => lot.Id, (link, lot) => lot)
            .Where(lot => lot.Status != InventoryLotStatus.Open)
            .Select(lot => lot.LotNumber)
            .Distinct()
            .ToListAsync();

        if (problematicLotNumbers.Count > 0)
        {
            throw new ConflictException(
                $"Cannot edit this purchase return because its source lot(s) are not open: {string.Join(", ", problematicLotNumbers)}.");
        }
    }

    private async Task<ValidatedPurchaseReturnRequest> ValidateRequestAsync(CreatePurchaseReturnRequest request, int? existingVoucherId = null)
    {
        if (request == null)
            throw new RequestValidationException("Request payload is required.");

        if (request.Lines == null)
            throw new RequestValidationException("Product lines are required.");

        if (request.VendorId <= 0)
            throw new RequestValidationException("Select a valid vendor.");

        if (request.Lines.Count == 0)
            throw new RequestValidationException("Add at least one product line.");

        foreach (var line in request.Lines)
        {
            if (line.SelectedLotId <= 0)
                throw new RequestValidationException("Each line must select a source lot.");

            if (line.ProductId <= 0)
                throw new RequestValidationException("Each line must reference a valid product.");

            if (line.Qty <= 0)
                throw new RequestValidationException("Each line must have a quantity greater than zero.");

            if (line.TotalWeightKg <= 0)
                throw new RequestValidationException("Each line must have a total weight greater than zero.");

            if (line.Rate < 0)
                throw new RequestValidationException("Rates cannot be negative.");
        }

        var accountIds = request.Lines
            .Select(line => line.ProductId)
            .Append(request.VendorId)
            .Distinct()
            .ToList();

        var accountsById = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (!accountsById.TryGetValue(request.VendorId, out var partyAccount))
            throw new RequestValidationException("Select a valid vendor.");

        EnsurePartyAccount(partyAccount, PartyRole.Vendor, "vendor");

        var productAccounts = new Dictionary<int, Account>();
        foreach (var productId in request.Lines.Select(line => line.ProductId).Distinct())
        {
            if (!accountsById.TryGetValue(productId, out var productAccount)
                || !IsInventoryAccount(productAccount)
                || !HasValidPurchaseInventorySetup(productAccount))
                throw new RequestValidationException("One or more selected inventory items are invalid.");

            productAccounts[productId] = productAccount;
        }

        var lotIds = request.Lines.Select(line => line.SelectedLotId).Distinct().ToArray();
        var selectedLots = await _db.InventoryLots
            .Include(x => x.VendorAccount)
            .Include(x => x.ProductAccount)
            .Where(x => lotIds.Contains(x.Id) && x.Status == InventoryLotStatus.Open)
            .ToDictionaryAsync(x => x.Id);

        var availableByLot = await _inventoryLotService.GetAvailableWeightByLotIdsAsync(lotIds);
        if (existingVoucherId.HasValue)
        {
            var currentVoucherLoads = await _db.InventoryTransactions
                .Where(x => x.VoucherId == existingVoucherId.Value
                    && x.VoucherType == VoucherType.PurchaseReturnVoucher
                    && x.TransactionType == InventoryTransactionType.PurchaseReturnOut)
                .GroupBy(x => x.LotId)
                .Select(g => new
                {
                    g.Key,
                    Kg = g.Sum(x => x.WeightKgDelta < 0 ? -x.WeightKgDelta : x.WeightKgDelta)
                })
                .ToListAsync();

            foreach (var row in currentVoucherLoads)
            {
                availableByLot[row.Key] = VoucherHelpers.Round2(availableByLot.GetValueOrDefault(row.Key) + row.Kg);
            }
        }

        var requestedByLot = request.Lines
            .GroupBy(x => x.SelectedLotId)
            .ToDictionary(g => g.Key, g => VoucherHelpers.Round2(g.Sum(x => x.TotalWeightKg)));

        foreach (var line in request.Lines)
        {
            if (!selectedLots.TryGetValue(line.SelectedLotId, out var lot))
                throw new RequestValidationException("Each line must select a valid source lot.");
            if (lot.ProductAccountId != line.ProductId)
                throw new RequestValidationException("Selected lot does not match the chosen purchase return product.");
            if (lot.VendorAccountId != request.VendorId)
                throw new RequestValidationException("Selected lot does not belong to the chosen vendor.");
        }

        foreach (var pair in requestedByLot)
        {
            var available = VoucherHelpers.Round2(availableByLot.GetValueOrDefault(pair.Key));
            if (pair.Value > available)
                throw new RequestValidationException($"Requested return exceeds available lot balance of {available:0.##} kg.");
        }

        return new ValidatedPurchaseReturnRequest(productAccounts, partyAccount, selectedLots);
    }

    private static void EnsurePartyAccount(Account account, PartyRole requiredRole, string partyLabel)
    {
        if (account.AccountType != AccountType.Party || account.PartyDetail == null)
            throw new RequestValidationException($"Select a valid {partyLabel}.");

        var isValid = requiredRole switch
        {
            PartyRole.Vendor => account.PartyDetail.PartyRole is PartyRole.Vendor or PartyRole.Both,
            _ => false
        };

        if (!isValid)
            throw new RequestValidationException($"Selected account is not a valid {partyLabel}.");
    }

    private static bool IsInventoryAccount(Account account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static bool HasValidPurchaseInventorySetup(Account account) =>
        account.AccountType == AccountType.UnwashedMaterial || account.ProductDetail != null;

    private static decimal CalculateLineAmt(decimal totalWeightKg, decimal rate) =>
        VoucherHelpers.Round2(totalWeightKg * rate);

    private static PurchaseReturnDto MapToDto(JournalVoucher prJv, IReadOnlyDictionary<string, InventoryLot>? selectedLots)
    {
        var vendorEntry = prJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        var productEntries = prJv.Entries.Where(e => e.SortOrder > 0).OrderBy(e => e.SortOrder).ToList();

        var dto = new PurchaseReturnDto
        {
            Id = prJv.Id,
            PrNumber = prJv.VoucherNumber,
            Date = prJv.Date,
            VendorId = vendorEntry?.AccountId ?? 0,
            VendorName = vendorEntry?.Account?.Name,
            VehicleNumber = prJv.VehicleNumber,
            Description = prJv.Description,
            VoucherType = prJv.VoucherType.ToString(),
            RatesAdded = prJv.RatesAdded,
            Lines = productEntries.Select(e => new PurchaseReturnLineDto
            {
                Id = e.Id,
                SelectedLotId = selectedLots?.GetValueOrDefault($"purchase-return-line-{e.SortOrder}")?.Id,
                SelectedLotNumber = selectedLots?.GetValueOrDefault($"purchase-return-line-{e.SortOrder}")?.LotNumber,
                ProductId = e.AccountId,
                ProductName = e.Account?.Name,
                Packing = e.Account?.ProductDetail?.Packing,
                PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg ?? 0,
                Rbp = e.Rbp ?? "Yes",
                Qty = e.Qty ?? 0,
                TotalWeightKg = e.ActualWeightKg ?? 0,
                Rate = e.Rate ?? 0,
                SortOrder = e.SortOrder
            }).ToList(),
        };

        dto.JournalVouchers = [MapJvSummary(prJv)];

        return dto;
    }

    private static JournalVoucherSummaryDto MapJvSummary(JournalVoucher jv) => new()
    {
        Id = jv.Id,
        VoucherNumber = jv.VoucherNumber,
        VoucherType = jv.VoucherType.ToString(),
        RatesAdded = jv.RatesAdded,
        TotalDebit = jv.Entries.Sum(e => e.Debit),
        TotalCredit = jv.Entries.Sum(e => e.Credit),
        Entries = jv.Entries.OrderBy(e => e.SortOrder).Select(e => new JournalEntryDto
        {
            Id = e.Id,
            AccountId = e.AccountId,
            AccountName = e.Account?.Name,
            Description = e.Description,
            Debit = e.Debit,
            Credit = e.Credit,
            Qty = e.Qty,
            Rbp = e.Rbp,
            Rate = e.Rate,
            IsEdited = e.IsEdited,
            SortOrder = e.SortOrder
        }).ToList()
    };

    private sealed record ValidatedPurchaseReturnRequest(
        IReadOnlyDictionary<int, Account> ProductAccounts,
        Account PartyAccount,
        IReadOnlyDictionary<int, InventoryLot> SelectedLots);

    private async Task<CreatePurchaseReturnRequest> BuildUpdateRequestAsync(JournalVoucher voucher)
    {
        var selectedLots = await LoadSelectedLotsAsync([voucher.Id]);
        var byLineKey = selectedLots.GetValueOrDefault(voucher.Id) ?? new Dictionary<string, InventoryLot>();

        return new CreatePurchaseReturnRequest
        {
            Date = voucher.Date,
            VendorId = voucher.Entries.FirstOrDefault(e => e.SortOrder == 0)?.AccountId ?? 0,
            VehicleNumber = voucher.VehicleNumber,
            Description = voucher.Description,
            Lines = voucher.Entries
                .Where(e => e.SortOrder > 0)
                .OrderBy(e => e.SortOrder)
                .Select(e => new CreatePurchaseReturnLineRequest
                {
                    LineId = e.Id,
                    SelectedLotId = byLineKey.GetValueOrDefault($"purchase-return-line-{e.SortOrder}")?.Id ?? 0,
                    ProductId = e.AccountId,
                    Qty = e.Qty ?? 0,
                    TotalWeightKg = e.ActualWeightKg ?? 0m,
                    Rate = e.Rate ?? 0m,
                    SortOrder = e.SortOrder
                })
                .ToList()
        };
    }

    private async Task RebuildInventoryAsync(JournalVoucher voucher, CreatePurchaseReturnRequest request, ValidatedPurchaseReturnRequest validation)
    {
        await RemoveVoucherInventoryAsync(voucher.Id);

        var productEntries = voucher.Entries
            .Where(x => x.SortOrder > 0)
            .OrderBy(x => x.SortOrder)
            .ToList();
        var requestLines = request.Lines
            .OrderBy(x => x.SortOrder)
            .ToList();

        for (var index = 0; index < productEntries.Count; index++)
        {
            var entry = productEntries[index];
            var line = requestLines[index];
            var lot = validation.SelectedLots[line.SelectedLotId];

            var tx = new InventoryTransaction
            {
                VoucherId = voucher.Id,
                VoucherType = VoucherType.PurchaseReturnVoucher,
                VoucherLineKey = $"purchase-return-line-{entry.SortOrder}",
                TransactionType = InventoryTransactionType.PurchaseReturnOut,
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
                VoucherType = VoucherType.PurchaseReturnVoucher,
                VoucherLineKey = $"purchase-return-line-{entry.SortOrder}",
                LotId = lot.Id,
                TransactionId = tx.Id
            });
        }
    }

    private async Task RemoveVoucherInventoryAsync(int voucherId)
    {
        var links = await _db.InventoryVoucherLinks
            .Where(x => x.VoucherId == voucherId && x.VoucherType == VoucherType.PurchaseReturnVoucher)
            .ToListAsync();
        if (links.Count == 0)
            return;

        var transactionIds = links.Select(x => x.TransactionId).Distinct().ToList();
        var transactions = await _db.InventoryTransactions
            .Where(x => transactionIds.Contains(x.Id))
            .ToListAsync();

        _db.InventoryVoucherLinks.RemoveRange(links);
        _db.InventoryTransactions.RemoveRange(transactions);
    }

    private async Task<Dictionary<int, IReadOnlyDictionary<string, InventoryLot>>> LoadSelectedLotsAsync(IReadOnlyCollection<int> voucherIds)
    {
        var ids = voucherIds.Where(x => x > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var links = await _db.InventoryVoucherLinks
            .AsNoTracking()
            .Where(x => ids.Contains(x.VoucherId)
                && x.VoucherType == VoucherType.PurchaseReturnVoucher
                && x.VoucherLineKey.StartsWith("purchase-return-line-"))
            .ToListAsync();
        if (links.Count == 0)
            return [];

        var lotIds = links.Select(x => x.LotId).Distinct().ToArray();
        var lots = await _db.InventoryLots
            .AsNoTracking()
            .Include(x => x.VendorAccount)
            .Where(x => lotIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);

        return links
            .GroupBy(x => x.VoucherId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, InventoryLot>)g.ToDictionary(x => x.VoucherLineKey, x => lots[x.LotId]));
    }

    private async Task<PurchaseReturnDto?> GetVoucherAsync(System.Linq.Expressions.Expression<Func<JournalVoucher, bool>> predicate)
    {
        var prJv = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Where(predicate)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        if (prJv == null) return null;

        var selectedLots = await LoadSelectedLotsAsync([prJv.Id]);
        return MapToDto(prJv, selectedLots.GetValueOrDefault(prJv.Id));
    }
}
