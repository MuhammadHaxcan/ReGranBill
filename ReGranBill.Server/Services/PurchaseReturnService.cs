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

    public PurchaseReturnService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<PurchaseReturnDto>> GetAllAsync()
    {
        var prJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return prJvs.Select(MapToDto).ToList();
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

        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        prJv.Date = request.Date;
        prJv.VehicleNumber = request.VehicleNumber;
        prJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Return to", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        prJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        prJv.UpdatedAt = DateTime.UtcNow;

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
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var prJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.PurchaseReturnVoucher);

        if (prJv == null) return (false, null);

        if (prJv.RatesAdded)
            return (false, "Cannot delete a rated purchase return. Only pending returns can be deleted.");

        prJv.IsDeleted = true;
        prJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<ValidatedPurchaseReturnRequest> ValidateRequestAsync(CreatePurchaseReturnRequest request)
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
                || productAccount.ProductDetail == null)
                throw new RequestValidationException("One or more selected inventory items are invalid.");

            productAccounts[productId] = productAccount;
        }

        return new ValidatedPurchaseReturnRequest(productAccounts, partyAccount);
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

    private static decimal CalculateLineAmt(decimal totalWeightKg, decimal rate) =>
        VoucherHelpers.Round2(totalWeightKg * rate);

    private static PurchaseReturnDto MapToDto(JournalVoucher prJv)
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
        Account PartyAccount);

    private async Task<PurchaseReturnDto?> GetVoucherAsync(System.Linq.Expressions.Expression<Func<JournalVoucher, bool>> predicate)
    {
        var prJv = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.PurchaseReturnVoucher)
            .Where(predicate)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        return prJv == null ? null : MapToDto(prJv);
    }
}
