using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.MasterReport;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class MasterReportService : IMasterReportService
{
    private readonly AppDbContext _db;

    public MasterReportService(AppDbContext db) => _db = db;

    public async Task<MasterReportDto> GetReportAsync(DateOnly? from, DateOnly? to, int? categoryId, int? accountId)
    {
        var categoryName = categoryId.HasValue
            ? await _db.Categories.Where(c => c.Id == categoryId.Value).Select(c => c.Name).FirstOrDefaultAsync()
            : null;
        var accountName = accountId.HasValue
            ? await _db.Accounts.Where(a => a.Id == accountId.Value).Select(a => a.Name).FirstOrDefaultAsync()
            : null;

        var query = _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Include(e => e.Account)
            .Where(e =>
                (e.JournalVoucher.RatesAdded
                    || e.JournalVoucher.VoucherType == VoucherType.JournalVoucher)
                && !(e.JournalVoucher.VoucherType == VoucherType.CartageVoucher
                    && _db.JournalVoucherReferences.Any(r =>
                        r.ReferenceVoucherId == e.VoucherId
                        && !r.MainVoucher.RatesAdded)));

        if (from.HasValue)
            query = query.Where(e => e.JournalVoucher.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.JournalVoucher.Date <= to.Value);

        if (categoryId.HasValue)
            query = query.Where(e => e.Account.CategoryId == categoryId.Value);

        if (accountId.HasValue)
            query = query.Where(e => e.AccountId == accountId.Value);

        var entries = await query
            .OrderBy(e => e.JournalVoucher.Date)
            .ThenBy(e => e.JournalVoucher.Id)
            .ThenBy(e => e.SortOrder)
            .ToListAsync();

        decimal runningBalance = 0;
        var entryDtos = new List<MasterReportEntryDto>();

        foreach (var e in entries)
        {
            runningBalance += e.Debit - e.Credit;
            entryDtos.Add(new MasterReportEntryDto
            {
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherType = e.JournalVoucher.VoucherType.ToString(),
                Date = e.JournalVoucher.Date,
                Description = e.Description,
                AccountName = e.Account.Name,
                Quantity = e.Qty,
                Rate = e.Rate,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = runningBalance
            });
        }

        var accountSummaries = entries
            .GroupBy(entry => new
            {
                entry.AccountId,
                AccountName = entry.Account.Name
            })
            .Select(group =>
            {
                var totalDebit = group.Sum(entry => entry.Debit);
                var totalCredit = group.Sum(entry => entry.Credit);
                return new MasterReportAccountSummaryDto
                {
                    AccountId = group.Key.AccountId,
                    AccountName = group.Key.AccountName,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Balance = totalDebit - totalCredit
                };
            })
            .OrderBy(summary => summary.AccountName)
            .ThenBy(summary => summary.AccountId)
            .ToList();

        return new MasterReportDto
        {
            FromDate = from,
            ToDate = to,
            CategoryName = categoryName,
            AccountName = accountName,
            TotalEntries = entryDtos.Count,
            TotalDebit = entryDtos.Sum(e => e.Debit),
            TotalCredit = entryDtos.Sum(e => e.Credit),
            NetBalance = runningBalance,
            AccountSummaries = accountSummaries,
            Entries = entryDtos
        };
    }

}
