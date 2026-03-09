using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.MasterReport;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class MasterReportService : IMasterReportService
{
    private readonly AppDbContext _db;

    public MasterReportService(AppDbContext db) => _db = db;

    public async Task<MasterReportDto> GetReportAsync(DateTime? from, DateTime? to, int? categoryId, int? accountId)
    {
        var query = _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Include(e => e.Account)
            .Where(e => e.JournalVoucher.RatesAdded
                || e.JournalVoucher.VoucherType == VoucherType.JournalVoucher);

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
            TotalEntries = entryDtos.Count,
            TotalDebit = entryDtos.Sum(e => e.Debit),
            TotalCredit = entryDtos.Sum(e => e.Credit),
            NetBalance = runningBalance,
            Entries = entryDtos
        };
    }
}
