using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.JournalVouchers;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class JournalVoucherService : IJournalVoucherService
{
    private readonly AppDbContext _db;
    private readonly IVoucherNumberService _voucherNumberService;

    public JournalVoucherService(AppDbContext db, IVoucherNumberService voucherNumberService)
    {
        _db = db;
        _voucherNumberService = voucherNumberService;
    }

    public async Task<List<JournalVoucherDto>> GetAllAsync()
    {
        var vouchers = await _db.JournalVouchers
            .Where(v => v.VoucherType == VoucherType.JournalVoucher)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .OrderByDescending(v => v.Date)
            .ThenByDescending(v => v.Id)
            .ToListAsync();

        return vouchers.Select(MapToDto).ToList();
    }

    public async Task<JournalVoucherDto?> GetByIdAsync(int id)
    {
        var voucher = await _db.JournalVouchers
            .Where(v => v.Id == id && v.VoucherType == VoucherType.JournalVoucher)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync();

        return voucher == null ? null : MapToDto(voucher);
    }

    public Task<string> GetNextNumberAsync() =>
        _voucherNumberService.GetNextNumberPreviewAsync(VoucherSequenceKeys.JournalVoucher, "JV-");

    public async Task<JournalVoucherDto> CreateAsync(CreateJournalVoucherRequest request, int userId)
    {
        await ValidateRequestAsync(request);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        var nextNumber = await _voucherNumberService.ReserveNextNumberAsync(VoucherSequenceKeys.JournalVoucher, "JV-");
        var voucher = new JournalVoucher
        {
            VoucherNumber = nextNumber,
            Date = request.Date,
            VoucherType = VoucherType.JournalVoucher,
            Description = request.Description?.Trim(),
            RatesAdded = true,
            CreatedBy = userId
        };

        foreach (var (entry, index) in request.Entries.OrderBy(e => e.SortOrder).Select((entry, index) => (entry, index)))
        {
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = entry.AccountId,
                Description = entry.Description?.Trim(),
                Debit = entry.Debit,
                Credit = entry.Credit,
                IsEdited = false,
                SortOrder = index
            });
        }

        _db.JournalVouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return (await GetByIdAsync(voucher.Id))!;
    }

    public async Task<JournalVoucherDto?> UpdateAsync(int id, CreateJournalVoucherRequest request)
    {
        var voucher = await _db.JournalVouchers
            .Where(v => v.Id == id && v.VoucherType == VoucherType.JournalVoucher)
            .Include(v => v.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        await ValidateRequestAsync(request);

        voucher.Date = request.Date;
        voucher.Description = request.Description?.Trim();
        voucher.RatesAdded = true;
        voucher.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();

        foreach (var (entry, index) in request.Entries.OrderBy(e => e.SortOrder).Select((entry, index) => (entry, index)))
        {
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = entry.AccountId,
                Description = entry.Description?.Trim(),
                Debit = entry.Debit,
                Credit = entry.Credit,
                IsEdited = true,
                SortOrder = index
            });
        }

        await _db.SaveChangesAsync();
        return (await GetByIdAsync(voucher.Id))!;
    }

    private async Task ValidateRequestAsync(CreateJournalVoucherRequest request)
    {
        if (request.Entries.Count < 2)
            throw new RequestValidationException("At least 2 journal lines are required.");

        var accountIds = request.Entries.Select(e => e.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (accounts.Count != accountIds.Count)
            throw new RequestValidationException("One or more selected accounts are invalid.");

        foreach (var entry in request.Entries)
        {
            if (entry.Debit < 0 || entry.Credit < 0)
                throw new RequestValidationException("Debit and credit cannot be negative.");

            var hasDebit = entry.Debit > 0;
            var hasCredit = entry.Credit > 0;
            if (hasDebit == hasCredit)
                throw new RequestValidationException("Each line must have either debit or credit.");

            var account = accounts[entry.AccountId];
            var isAllowed = account.AccountType is AccountType.Expense or AccountType.Party or AccountType.Account;
            if (!isAllowed)
                throw new RequestValidationException("Only expense, party, and account types are allowed in journal vouchers.");
        }

        var totalDebit = VoucherHelpers.Round2(request.Entries.Sum(e => e.Debit));
        var totalCredit = VoucherHelpers.Round2(request.Entries.Sum(e => e.Credit));
        if (totalDebit != totalCredit)
            throw new RequestValidationException("Total debit and total credit must be equal.");
    }

    private static JournalVoucherDto MapToDto(JournalVoucher voucher) => new()
    {
        Id = voucher.Id,
        VoucherNumber = voucher.VoucherNumber,
        Date = voucher.Date,
        VoucherType = voucher.VoucherType.ToString(),
        Description = voucher.Description,
        RatesAdded = voucher.RatesAdded,
        TotalDebit = voucher.Entries.Sum(e => e.Debit),
        TotalCredit = voucher.Entries.Sum(e => e.Credit),
        Entries = voucher.Entries
            .OrderBy(e => e.SortOrder)
            .Select(e => new JournalVoucherEntryDto
            {
                Id = e.Id,
                AccountId = e.AccountId,
                AccountName = e.Account?.Name,
                Description = e.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                IsEdited = e.IsEdited,
                SortOrder = e.SortOrder
            })
            .ToList()
    };
}
