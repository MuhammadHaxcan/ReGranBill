using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.CustomerLedger;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class CustomerLedgerService : ICustomerLedgerService
{
    private readonly AppDbContext _db;

    public CustomerLedgerService(AppDbContext db) => _db = db;

    public async Task<CustomerLedgerDto?> GetLedgerAsync(int accountId, DateOnly? fromDate, DateOnly? toDate)
    {
        var account = await _db.Accounts
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account == null) return null;

        decimal openingBalance = 0m;
        if (fromDate.HasValue)
        {
            // Opening balance is only meaningful when a start date is selected.
            var openingEntries = await _db.JournalEntries
                .Include(e => e.JournalVoucher)
                .Where(e => e.AccountId == accountId)
                .Where(e => e.JournalVoucher.Date < fromDate.Value)
                .Where(e =>
                    (e.JournalVoucher.RatesAdded || e.JournalVoucher.VoucherType == VoucherType.JournalVoucher)
                    && !(e.JournalVoucher.VoucherType == VoucherType.CartageVoucher
                        && _db.JournalVoucherReferences.Any(r =>
                            r.ReferenceVoucherId == e.VoucherId
                            && !r.MainVoucher.RatesAdded)))
                .ToListAsync();

            openingBalance = openingEntries.Sum(e => e.Debit - e.Credit);
        }

        // Filter for date range
        var voucherQuery = _db.JournalVouchers
            .Where(v => !fromDate.HasValue || v.Date >= fromDate.Value)
            .Where(v => !toDate.HasValue || v.Date <= toDate.Value)
            .Where(v => v.RatesAdded || v.VoucherType == VoucherType.JournalVoucher);

        // Get all vouchers that involve this account in the selected range.
        var voucherIds = await _db.JournalEntries
            .Where(e => e.AccountId == accountId)
            .Where(e => voucherQuery.Any(v => v.Id == e.VoucherId))
            .Select(e => e.VoucherId)
            .Distinct()
            .ToListAsync();

        // Pull all entries for matching vouchers so we can expand item-linked vouchers
        // into per-item ledger rows while keeping non-item vouchers unchanged.
        var voucherEntries = await _db.JournalEntries
            .Include(e => e.JournalVoucher)
            .Include(e => e.Account)
                .ThenInclude(a => a.ProductDetail)
            .Where(e => voucherIds.Contains(e.VoucherId))
            .OrderBy(e => e.JournalVoucher.Date)
            .ThenBy(e => e.JournalVoucher.Id)
            .ThenBy(e => e.SortOrder)
            .ToListAsync();

        // Compute running balance
        decimal runningBalance = openingBalance;
        var entryDtos = new List<CustomerLedgerEntryDto>();
        var groupedByVoucher = voucherEntries
            .GroupBy(e => e.VoucherId)
            .OrderBy(g => g.Min(e => e.JournalVoucher.Date))
            .ThenBy(g => g.Key)
            .ToList();

        foreach (var voucherGroup in groupedByVoucher)
        {
            var orderedVoucherEntries = voucherGroup.OrderBy(e => e.SortOrder).ToList();
            var partyEntries = orderedVoucherEntries.Where(e => e.AccountId == accountId).ToList();
            if (partyEntries.Count == 0)
            {
                continue;
            }

            var hasItemLinkedRows = orderedVoucherEntries
                .Any(e => e.SortOrder > 0 && IsItemLinkedEntry(e));

            // Washing vouchers have weight/rate on the unwashed-input credit and washed-output debit,
            // but those are internal inventory transfers — not trade with the queried party.
            // The only party-relevant line is the vendor entry itself (e.g. excess wastage charged back).
            // Emit it directly without expanding the internal item-linked rows.
            var skipItemExpansion = orderedVoucherEntries.Count > 0
                && orderedVoucherEntries[0].JournalVoucher.VoucherType == VoucherType.WashingVoucher;

            if (!hasItemLinkedRows || skipItemExpansion)
            {
                foreach (var partyEntry in partyEntries)
                {
                    runningBalance += partyEntry.Debit - partyEntry.Credit;
                    entryDtos.Add(new CustomerLedgerEntryDto
                    {
                        EntryId = partyEntry.Id,
                        VoucherId = partyEntry.VoucherId,
                        VoucherType = partyEntry.JournalVoucher.VoucherType.ToString(),
                        VoucherNumber = partyEntry.JournalVoucher.VoucherNumber,
                        Date = partyEntry.JournalVoucher.Date,
                        Description = partyEntry.Description ?? partyEntry.JournalVoucher.Description,
                        ProductName = partyEntry.Account.Name,
                        Packing = partyEntry.Account.ProductDetail?.Packing,
                        Qty = partyEntry.Qty,
                        Weight = ResolveWeight(partyEntry),
                        Rate = partyEntry.Rate,
                        Debit = partyEntry.Debit,
                        Credit = partyEntry.Credit,
                        RunningBalance = runningBalance
                    });
                }

                continue;
            }

            var itemEntries = orderedVoucherEntries
                .Where(e => e.SortOrder > 0 && IsItemLinkedEntry(e))
                .ToList();

            // Party side determines how itemized amounts appear in ledger (debit vs credit).
            var partySide = partyEntries
                .Select(e => new { e.Debit, e.Credit })
                .FirstOrDefault(e => e.Debit > 0 || e.Credit > 0);

            var useDebitSide = partySide?.Debit > 0;
            var useCreditSide = !useDebitSide;

            foreach (var itemEntry in itemEntries)
            {
                var amount = itemEntry.Debit > 0 ? itemEntry.Debit : itemEntry.Credit;
                var debit = useDebitSide ? amount : 0m;
                var credit = useCreditSide ? amount : 0m;

                runningBalance += debit - credit;
                entryDtos.Add(new CustomerLedgerEntryDto
                {
                    EntryId = itemEntry.Id,
                    VoucherId = itemEntry.VoucherId,
                    VoucherType = itemEntry.JournalVoucher.VoucherType.ToString(),
                    VoucherNumber = itemEntry.JournalVoucher.VoucherNumber,
                    Date = itemEntry.JournalVoucher.Date,
                    Description = itemEntry.Description ?? itemEntry.JournalVoucher.Description,
                    ProductName = itemEntry.Account.Name,
                    Packing = itemEntry.Account.ProductDetail?.Packing,
                    Qty = itemEntry.Qty,
                    Weight = ResolveWeight(itemEntry),
                    Rate = itemEntry.Rate,
                    Debit = debit,
                    Credit = credit,
                    RunningBalance = runningBalance
                });
            }
        }

        return new CustomerLedgerDto
        {
            AccountId = account.Id,
            AccountName = account.Name,
            PartyType = account.PartyDetail?.PartyRole.ToString() ?? "",
            HasOpeningBalance = fromDate.HasValue,
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            Entries = entryDtos
        };
    }

    private static bool IsItemLinkedEntry(Entities.JournalEntry entry) =>
        entry.Qty.HasValue
        || entry.ActualWeightKg.HasValue
        || entry.Rate.HasValue
        || entry.Account.ProductDetail != null;

    private static decimal? ResolveWeight(Entities.JournalEntry entry)
    {
        if (entry.ActualWeightKg.HasValue)
        {
            return entry.ActualWeightKg.Value;
        }

        if (!entry.Qty.HasValue)
        {
            return null;
        }

        var qty = entry.Qty.Value;
        var usePackedWeight = string.Equals(entry.Rbp, "Yes", StringComparison.OrdinalIgnoreCase);
        if (!usePackedWeight)
        {
            return VoucherHelpers.Round2(qty);
        }

        var packingWeight = entry.Account.ProductDetail?.PackingWeightKg;
        if (!packingWeight.HasValue)
        {
            return null;
        }

        return VoucherHelpers.Round2(packingWeight.Value * qty);
    }

    public async Task<List<CustomerLedgerDto>> GetAllLedgersAsync(string partyType, DateOnly? fromDate, DateOnly? toDate)
    {
        if (!Enum.TryParse<PartyRole>(partyType, true, out var requestedRole))
        {
            throw new RequestValidationException("Select a valid party type.");
        }

        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => a.PartyDetail != null)
            .Where(a => MatchesRequestedPartyRole(a.PartyDetail!.PartyRole, requestedRole))
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

    private static bool MatchesRequestedPartyRole(PartyRole actualRole, PartyRole requestedRole) =>
        requestedRole switch
        {
            PartyRole.Customer => actualRole is PartyRole.Customer or PartyRole.Both,
            PartyRole.Vendor => actualRole is PartyRole.Vendor or PartyRole.Both,
            PartyRole.Transporter => actualRole is PartyRole.Transporter or PartyRole.Both,
            PartyRole.Both => actualRole == PartyRole.Both,
            _ => false
        };
}
