using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.AccountClosingReport;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class AccountClosingReportService : IAccountClosingReportService
{
    private readonly AppDbContext _db;

    public AccountClosingReportService(AppDbContext db) => _db = db;

    public async Task<AccountClosingReportDto> GetReportAsync(DateOnly? from, DateOnly? to, int? accountId, int? historyAccountId = null)
    {
        var fromDate = VoucherHelpers.ToUtcStartOfDay(from);
        var toExclusiveDate = VoucherHelpers.ToUtcStartOfDay(to?.AddDays(1));
        var historyTargetId = historyAccountId ?? accountId;

        var accountQuery = _db.Accounts
            .AsNoTracking()
            .Where(account => account.AccountType == AccountType.Account);

        if (accountId.HasValue)
        {
            accountQuery = accountQuery.Where(account => account.Id == accountId.Value);
        }

        var accounts = await accountQuery
            .OrderBy(account => account.Name)
            .Select(account => new { account.Id, account.Name })
            .ToListAsync();

        var accountIds = accounts.Select(account => account.Id).ToList();
        if (accountIds.Count == 0)
        {
            return new AccountClosingReportDto
            {
                FromDate = fromDate,
                ToDate = toExclusiveDate?.AddDays(-1),
                SelectedAccountId = accountId,
                HistoryAccountId = historyTargetId
            };
        }

        var entryQuery = _db.JournalEntries
            .AsNoTracking()
            .Include(entry => entry.JournalVoucher)
            .Include(entry => entry.Account)
            .Where(entry => accountIds.Contains(entry.AccountId))
            .Where(entry =>
                (entry.JournalVoucher.RatesAdded || entry.JournalVoucher.VoucherType == VoucherType.JournalVoucher) &&
                !(entry.JournalVoucher.VoucherType == VoucherType.CartageVoucher &&
                  _db.JournalVoucherReferences.Any(reference =>
                      reference.ReferenceVoucherId == entry.VoucherId &&
                      !reference.MainVoucher.RatesAdded)));

        if (toExclusiveDate.HasValue)
        {
            entryQuery = entryQuery.Where(entry => entry.JournalVoucher.Date < toExclusiveDate.Value);
        }

        var entries = await entryQuery
            .OrderBy(entry => entry.JournalVoucher.Date)
            .ThenBy(entry => entry.JournalVoucher.Id)
            .ThenBy(entry => entry.SortOrder)
            .ToListAsync();

        var summaryRows = new List<AccountClosingSummaryRowDto>();

        foreach (var account in accounts)
        {
            var accountEntries = entries.Where(entry => entry.AccountId == account.Id).ToList();
            var openingBalance = fromDate.HasValue
                ? accountEntries
                    .Where(entry => entry.JournalVoucher.Date < fromDate.Value)
                    .Sum(entry => entry.Debit - entry.Credit)
                : 0m;

            var periodEntries = accountEntries
                .Where(entry => !fromDate.HasValue || entry.JournalVoucher.Date >= fromDate.Value)
                .ToList();

            var periodDebit = periodEntries.Sum(entry => entry.Debit);
            var periodCredit = periodEntries.Sum(entry => entry.Credit);
            var closingBalance = openingBalance + periodDebit - periodCredit;

            summaryRows.Add(new AccountClosingSummaryRowDto
            {
                AccountId = account.Id,
                AccountName = account.Name,
                OpeningBalance = VoucherHelpers.Round2(openingBalance),
                PeriodDebit = VoucherHelpers.Round2(periodDebit),
                PeriodCredit = VoucherHelpers.Round2(periodCredit),
                ClosingBalance = VoucherHelpers.Round2(closingBalance)
            });
        }

        var selectedAccountName = accountId.HasValue
            ? accounts.FirstOrDefault(account => account.Id == accountId.Value)?.Name
            : null;
        var historyAccountName = historyTargetId.HasValue
            ? accounts.FirstOrDefault(account => account.Id == historyTargetId.Value)?.Name
            : null;

        var history = new List<AccountClosingHistoryEntryDto>();
        if (historyTargetId.HasValue && accountIds.Contains(historyTargetId.Value))
        {
            var selectedEntries = entries
                .Where(entry => entry.AccountId == historyTargetId.Value)
                .Where(entry => !fromDate.HasValue || entry.JournalVoucher.Date >= fromDate.Value)
                .OrderBy(entry => entry.JournalVoucher.Date)
                .ThenBy(entry => entry.JournalVoucher.Id)
                .ThenBy(entry => entry.SortOrder)
                .ToList();

            var openingBalance = summaryRows
                .First(row => row.AccountId == historyTargetId.Value)
                .OpeningBalance;

            var runningBalance = openingBalance;
            foreach (var entry in selectedEntries)
            {
                runningBalance += entry.Debit - entry.Credit;
                history.Add(new AccountClosingHistoryEntryDto
                {
                    EntryId = entry.Id,
                    VoucherId = entry.VoucherId,
                    VoucherNumber = entry.JournalVoucher.VoucherNumber,
                    VoucherType = entry.JournalVoucher.VoucherType.ToString(),
                    Date = entry.JournalVoucher.Date,
                    Description = entry.Description,
                    Quantity = entry.Qty,
                    Rate = entry.Rate,
                    Debit = entry.Debit,
                    Credit = entry.Credit,
                    RunningBalance = VoucherHelpers.Round2(runningBalance)
                });
            }
        }

        return new AccountClosingReportDto
        {
            FromDate = fromDate,
            ToDate = toExclusiveDate?.AddDays(-1),
            SelectedAccountId = accountId,
            SelectedAccountName = selectedAccountName,
            HistoryAccountId = historyTargetId,
            HistoryAccountName = historyAccountName,
            TotalAccounts = summaryRows.Count,
            TotalOpeningBalance = VoucherHelpers.Round2(summaryRows.Sum(row => row.OpeningBalance)),
            TotalDebit = VoucherHelpers.Round2(summaryRows.Sum(row => row.PeriodDebit)),
            TotalCredit = VoucherHelpers.Round2(summaryRows.Sum(row => row.PeriodCredit)),
            TotalClosingBalance = VoucherHelpers.Round2(summaryRows.Sum(row => row.ClosingBalance)),
            Accounts = summaryRows,
            History = history
        };
    }

}
