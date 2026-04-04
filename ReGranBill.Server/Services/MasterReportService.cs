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
        var fromDate = VoucherHelpers.ToUtcStartOfDay(from);
        var toExclusiveDate = VoucherHelpers.ToUtcStartOfDay(to?.AddDays(1));
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

        if (fromDate.HasValue)
            query = query.Where(e => e.JournalVoucher.Date >= fromDate.Value);

        if (toExclusiveDate.HasValue)
            query = query.Where(e => e.JournalVoucher.Date < toExclusiveDate.Value);

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

        return new MasterReportDto
        {
            FromDate = fromDate,
            ToDate = VoucherHelpers.ToUtcStartOfDay(to),
            CategoryName = categoryName,
            AccountName = accountName,
            TotalEntries = entryDtos.Count,
            TotalDebit = entryDtos.Sum(e => e.Debit),
            TotalCredit = entryDtos.Sum(e => e.Credit),
            NetBalance = runningBalance,
            Entries = entryDtos
        };
    }

}
