using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.SaleReturns;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class SaleReturnService : ISaleReturnService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public SaleReturnService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<SaleReturnDto>> GetAllAsync()
    {
        var saleReturnJvs = await _db.JournalVouchers
            .Where(j => j.VoucherType == VoucherType.SaleReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return saleReturnJvs.Select(MapToDto).ToList();
    }

    public async Task<SaleReturnDto?> GetByIdAsync(int id)
    {
        var saleReturnJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.PartyDetail)
            .FirstOrDefaultAsync();

        return saleReturnJv == null ? null : MapToDto(saleReturnJv);
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.SaleReturn, "SR-");

    public async Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds)
    {
        var ids = productIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
            return [];

        var entries = await _db.JournalEntries
            .AsNoTracking()
            .Where(e =>
                e.SortOrder > 0 &&
                ids.Contains(e.AccountId) &&
                e.Rate.HasValue &&
                e.Rate.Value > 0 &&
                e.JournalVoucher.VoucherType == VoucherType.SaleVoucher)
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

    public async Task<SaleReturnDto> CreateAsync(CreateSaleReturnRequest request, int userId)
    {
        var validation = await ValidateRequestAsync(request);
        var srDate = request.Date;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var srNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.SaleReturn, "SR-");

        var saleReturnJv = new JournalVoucher
        {
            VoucherNumber = srNumber,
            Date = srDate,
            VoucherType = VoucherType.SaleReturnVoucher,
            VehicleNumber = null,
            Description = VoucherHelpers.ResolveDescription(request.Description, "Return from", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts),
            RatesAdded = request.Lines.All(l => l.Rate > 0),
            CreatedBy = userId,
        };

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = VoucherHelpers.CalculateLineAmount(product.ProductDetail?.PackingWeightKg ?? 0, line.Rbp, line.Qty, line.Rate);
            totalProductAmount += lineAmount;

            saleReturnJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                Rbp = VoucherHelpers.NormalizeRbp(line.Rbp),
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = false,
                SortOrder = ++sortOrder
            });
        }

        saleReturnJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Return from customer - {srNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = false,
            SortOrder = 0
        });

        _db.JournalVouchers.Add(saleReturnJv);

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(saleReturnJv.Id))!;
    }

    public async Task<SaleReturnDto?> UpdateAsync(int id, CreateSaleReturnRequest request)
    {
        var saleReturnJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleReturnVoucher)
            .Include(j => j.Entries)
            .FirstOrDefaultAsync();

        if (saleReturnJv == null) return null;

        var validation = await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        saleReturnJv.Date = request.Date;
        saleReturnJv.VehicleNumber = null;
        saleReturnJv.Description = VoucherHelpers.ResolveDescription(request.Description, "Return from", validation.PartyAccount.Name, request.Lines.Select(l => (l.ProductId, l.SortOrder, l.Qty)), validation.ProductAccounts);
        saleReturnJv.RatesAdded = request.Lines.All(l => l.Rate > 0);
        saleReturnJv.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(saleReturnJv.Entries);
        saleReturnJv.Entries.Clear();

        var totalProductAmount = 0m;
        var sortOrder = 0;

        foreach (var line in request.Lines.OrderBy(l => l.SortOrder))
        {
            var product = validation.ProductAccounts[line.ProductId];
            var lineAmount = VoucherHelpers.CalculateLineAmount(product.ProductDetail?.PackingWeightKg ?? 0, line.Rbp, line.Qty, line.Rate);
            totalProductAmount += lineAmount;

            saleReturnJv.Entries.Add(new JournalEntry
            {
                AccountId = line.ProductId,
                Description = $"{product.Name} - {line.Qty} bags",
                Debit = lineAmount,
                Credit = 0,
                Qty = line.Qty,
                Rbp = VoucherHelpers.NormalizeRbp(line.Rbp),
                Rate = line.Rate > 0 ? line.Rate : null,
                IsEdited = true,
                SortOrder = ++sortOrder
            });
        }

        saleReturnJv.Entries.Add(new JournalEntry
        {
            AccountId = validation.PartyAccount.Id,
            Description = $"Return from customer - {saleReturnJv.VoucherNumber}",
            Debit = 0,
            Credit = totalProductAmount,
            IsEdited = true,
            SortOrder = 0
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(saleReturnJv.Id))!;
    }

    public async Task<bool> UpdateRatesAsync(int id, UpdateSaleReturnRatesRequest request)
    {
        var saleReturnJv = await _db.JournalVouchers
            .Where(j => j.Id == id && j.VoucherType == VoucherType.SaleReturnVoucher)
            .Include(j => j.Entries).ThenInclude(e => e.Account).ThenInclude(a => a.ProductDetail)
            .FirstOrDefaultAsync();

        if (saleReturnJv == null) return false;

        foreach (var update in request.Lines)
        {
            var entry = saleReturnJv.Entries.FirstOrDefault(e => e.Id == update.EntryId);
            if (entry != null && entry.SortOrder > 0)
            {
                entry.Rate = update.Rate;
                entry.IsEdited = true;

                var weight = entry.Account?.ProductDetail?.PackingWeightKg ?? 0;
                entry.Debit = VoucherHelpers.CalculateLineAmount(weight, entry.Rbp, entry.Qty ?? 0, update.Rate);
            }
        }

        var customerEntry = saleReturnJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        if (customerEntry != null)
        {
            customerEntry.Credit = saleReturnJv.Entries.Where(e => e.SortOrder > 0).Sum(e => e.Debit);
            customerEntry.IsEdited = true;
        }

        saleReturnJv.RatesAdded = saleReturnJv.Entries.Where(e => e.SortOrder > 0).All(e => e.Rate > 0);
        saleReturnJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? Error)> SoftDeleteAsync(int id)
    {
        var saleReturnJv = await _db.JournalVouchers
            .FirstOrDefaultAsync(j => j.Id == id && j.VoucherType == VoucherType.SaleReturnVoucher);

        if (saleReturnJv == null) return (false, null);

        if (saleReturnJv.RatesAdded)
            return (false, "Cannot delete a rated sale return. Only pending returns can be deleted.");

        saleReturnJv.IsDeleted = true;
        saleReturnJv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task<ValidatedSaleReturnRequest> ValidateRequestAsync(CreateSaleReturnRequest request)
    {
        if (request.CustomerId <= 0)
            throw new RequestValidationException("Select a valid customer.");

        if (request.Lines.Count == 0)
            throw new RequestValidationException("Add at least one product line.");

        foreach (var line in request.Lines)
        {
            if (line.ProductId <= 0)
                throw new RequestValidationException("Each line must reference a valid product.");

            if (line.Qty <= 0)
                throw new RequestValidationException("Each line must have a quantity greater than zero.");

            if (line.Rate < 0)
                throw new RequestValidationException("Rates cannot be negative.");

            if (!VoucherHelpers.IsValidRbp(line.Rbp))
                throw new RequestValidationException("RBP must be either Yes or No.");
        }

        var accountIds = request.Lines
            .Select(line => line.ProductId)
            .Append(request.CustomerId)
            .Distinct()
            .ToList();

        var accountsById = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (!accountsById.TryGetValue(request.CustomerId, out var partyAccount))
            throw new RequestValidationException("Select a valid customer.");

        EnsurePartyAccount(partyAccount, PartyRole.Customer, "customer");

        var productAccounts = new Dictionary<int, Account>();
        foreach (var productId in request.Lines.Select(line => line.ProductId).Distinct())
        {
            if (!accountsById.TryGetValue(productId, out var productAccount)
                || !IsInventoryAccount(productAccount)
                || productAccount.ProductDetail == null)
                throw new RequestValidationException("One or more selected inventory items are invalid.");

            productAccounts[productId] = productAccount;
        }

        return new ValidatedSaleReturnRequest(productAccounts, partyAccount);
    }

    private static void EnsurePartyAccount(Account account, PartyRole requiredRole, string partyLabel)
    {
        if (account.AccountType != AccountType.Party || account.PartyDetail == null)
            throw new RequestValidationException($"Select a valid {partyLabel}.");

        var isValid = requiredRole switch
        {
            PartyRole.Customer => account.PartyDetail.PartyRole is PartyRole.Customer or PartyRole.Both,
            PartyRole.Vendor => account.PartyDetail.PartyRole is PartyRole.Vendor or PartyRole.Both,
            _ => false
        };

        if (!isValid)
            throw new RequestValidationException($"Selected account is not a valid {partyLabel}.");
    }

    private static bool IsInventoryAccount(Account account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static SaleReturnDto MapToDto(JournalVoucher saleReturnJv)
    {
        var customerEntry = saleReturnJv.Entries.FirstOrDefault(e => e.SortOrder == 0);
        var productEntries = saleReturnJv.Entries.Where(e => e.SortOrder > 0).OrderBy(e => e.SortOrder).ToList();

        var dto = new SaleReturnDto
        {
            Id = saleReturnJv.Id,
            SrNumber = saleReturnJv.VoucherNumber,
            Date = saleReturnJv.Date,
            CustomerId = customerEntry?.AccountId ?? 0,
            CustomerName = customerEntry?.Account?.Name,
            VehicleNumber = saleReturnJv.VehicleNumber,
            Description = saleReturnJv.Description,
            VoucherType = saleReturnJv.VoucherType.ToString(),
            RatesAdded = saleReturnJv.RatesAdded,
            Lines = productEntries.Select(e => new SrLineDto
            {
                Id = e.Id,
                ProductId = e.AccountId,
                ProductName = e.Account?.Name,
                Packing = e.Account?.ProductDetail?.Packing,
                PackingWeightKg = e.Account?.ProductDetail?.PackingWeightKg ?? 0,
                Rbp = e.Rbp ?? "Yes",
                Qty = e.Qty ?? 0,
                Rate = e.Rate ?? 0,
                SortOrder = e.SortOrder
            }).ToList(),
        };

        dto.JournalVouchers = [MapJvSummary(saleReturnJv)];

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

    private sealed record ValidatedSaleReturnRequest(
        IReadOnlyDictionary<int, Account> ProductAccounts,
        Account PartyAccount);
}