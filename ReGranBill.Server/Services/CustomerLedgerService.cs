using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.CustomerLedger;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class CustomerLedgerService : ICustomerLedgerService
{
    private readonly AppDbContext _db;

    public CustomerLedgerService(AppDbContext db) => _db = db;

    public async Task<CustomerLedgerDto?> GetLedgerAsync(int accountId, DateOnly fromDate, DateOnly toDate)
    {
        var account = await _db.Accounts
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null) return null;

        var fromUtc = VoucherHelpers.ToUtcStartOfDay(fromDate);
        var toExclusiveUtc = VoucherHelpers.ToUtcStartOfDay(toDate.AddDays(1));

        // Opening balance: sum of (debit - credit) for all entries before fromDate
        var openingEntries = await _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Where(e => e.AccountId == accountId)
            .Where(e => e.JournalVoucher.Date < fromUtc)
            .Where(e => e.JournalVoucher.RatesAdded || e.JournalVoucher.VoucherType == VoucherType.JournalVoucher)
            .ToListAsync();

        var openingBalance = openingEntries.Sum(e => e.Debit - e.Credit);

        // Filter for date range
        var voucherQuery = _db.JournalVouchers
            .Where(v => v.Date >= fromUtc && v.Date < toExclusiveUtc)
            .Where(v => v.RatesAdded || v.VoucherType == VoucherType.JournalVoucher);

        // Build entry query: include sale/purchase/return entries for this account,
        // and receipt/payment entries for this account (SortOrder 0 = party)
        var entryQuery = _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Include(e => e.Account)
            .Where(e => e.AccountId == accountId)
            .Where(e => voucherQuery.Any(v => v.Id == e.VoucherId));

        var entries = await entryQuery
            .OrderBy(e => e.JournalVoucher.Date)
            .ThenBy(e => e.JournalVoucher.Id)
            .ThenBy(e => e.SortOrder)
            .ToListAsync();

        // Compute running balance
        decimal runningBalance = openingBalance;
        var entryDtos = new List<CustomerLedgerEntryDto>();

        foreach (var e in entries)
        {
            runningBalance += e.Debit - e.Credit;
            entryDtos.Add(new CustomerLedgerEntryDto
            {
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                VoucherType = e.JournalVoucher.VoucherType.ToString(),
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                Date = e.JournalVoucher.Date,
                Description = e.Description ?? e.JournalVoucher.Description,
                ProductName = e.Account.Name,
                Packing = e.Account.ProductDetail?.Packing,
                Qty = e.Qty,
                Weight = e.ActualWeightKg,
                Rate = e.Rate,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = runningBalance
            });
        }

        return new CustomerLedgerDto
        {
            AccountId = account.Id,
            AccountName = account.Name,
            PartyType = account.PartyDetail?.PartyRole.ToString() ?? "",
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            Entries = entryDtos
        };
    }

    public async Task<List<CustomerLedgerDto>> GetAllLedgersAsync(string partyType, DateOnly fromDate, DateOnly toDate)
    {
        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => a.PartyDetail != null)
            .Where(a => a.PartyDetail!.PartyRole.ToString() == partyType)
            .ToListAsync();

        var ledgers = new List<CustomerLedgerDto>();
        foreach (var account in accounts)
        {
            var ledger = await GetLedgerAsync(account.Id, fromDate, toDate);
            if (ledger != null && ledger.Entries.Count > 0)
                ledgers.Add(ledger);
        }
        return ledgers;
    }
}