using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.SOA;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class StatementService : IStatementService
{
    private readonly AppDbContext _db;

    public StatementService(AppDbContext db) => _db = db;

    public async Task<StatementOfAccountDto?> GetStatementAsync(int accountId, DateOnly? from, DateOnly? to)
    {
        var fromDate = VoucherHelpers.ToUtcStartOfDay(from);
        var toExclusiveDate = VoucherHelpers.ToUtcStartOfDay(to?.AddDays(1));

        var account = await _db.Accounts
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null) return null;

        var query = _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Where(e => e.AccountId == accountId)
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

        var entries = await query
            .OrderBy(e => e.JournalVoucher.Date)
            .ThenBy(e => e.JournalVoucher.Id)
            .ThenBy(e => e.SortOrder)
            .ToListAsync();

        // Compute running balance
        decimal runningBalance = 0;
        var entryDtos = new List<StatementEntryDto>();

        foreach (var e in entries)
        {
            runningBalance += e.Debit - e.Credit;
            entryDtos.Add(new StatementEntryDto
            {
                EntryId = e.Id,
                VoucherId = e.VoucherId,
                Date = e.JournalVoucher.Date,
                VoucherNumber = e.JournalVoucher.VoucherNumber,
                VoucherType = e.JournalVoucher.VoucherType.ToString(),
                Description = e.JournalVoucher.Description,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = runningBalance
            });
        }

        return new StatementOfAccountDto
        {
            AccountId = account.Id,
            AccountName = account.Name,
            PartyRole = account.PartyDetail?.PartyRole.ToString(),
            ContactPerson = account.PartyDetail?.ContactPerson,
            Phone = account.PartyDetail?.Phone,
            City = account.PartyDetail?.City,
            Address = account.PartyDetail?.Address,
            FromDate = VoucherHelpers.ToUtcStartOfDay(from),
            ToDate = VoucherHelpers.ToUtcStartOfDay(to),
            TotalDebit = entryDtos.Sum(e => e.Debit),
            TotalCredit = entryDtos.Sum(e => e.Credit),
            NetBalance = runningBalance,
            Entries = entryDtos
        };
    }

}
