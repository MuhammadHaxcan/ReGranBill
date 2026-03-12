using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.VoucherEditor;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class VoucherEditorService : IVoucherEditorService
{
    private readonly AppDbContext _db;

    public VoucherEditorService(AppDbContext db) => _db = db;

    public async Task<VoucherLedgerDto?> FindByTypeAndNumberAsync(string voucherType, string voucherNumber)
    {
        if (!TryParseVoucherType(voucherType, out var parsedType))
            throw new InvalidOperationException("Invalid voucher type.");

        var number = voucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number))
            throw new InvalidOperationException("Voucher number is required.");

        var voucher = await _db.JournalVouchers
            .Where(v => v.VoucherType == parsedType && v.VoucherNumber == number)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync();

        return voucher == null ? null : MapToDto(voucher);
    }

    public async Task<VoucherLedgerDto?> UpdateAsync(UpdateVoucherLedgerRequest request)
    {
        if (!TryParseVoucherType(request.VoucherType, out var voucherType))
            throw new InvalidOperationException("Invalid voucher type.");

        var voucherNumber = request.VoucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new InvalidOperationException("Voucher number is required.");

        var voucher = await _db.JournalVouchers
            .Where(v => v.VoucherType == voucherType && v.VoucherNumber == voucherNumber)
            .Include(v => v.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        var accountsById = await ValidateUpdateRequestAsync(voucherType, request);

        voucher.Date = request.Date;
        voucher.Description = ToNullIfWhiteSpace(request.Description);
        voucher.VehicleNumber = voucherType == VoucherType.SaleVoucher
            ? ToNullIfWhiteSpace(request.VehicleNumber)
            : null;
        voucher.UpdatedAt = DateTime.UtcNow;
        voucher.RatesAdded = ComputeRatesAdded(voucherType, request.Entries, accountsById, voucher.RatesAdded);

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();

        foreach (var (line, index) in request.Entries.OrderBy(e => e.SortOrder).Select((line, index) => (line, index)))
        {
            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = ToNullIfWhiteSpace(line.Description),
                Debit = Round2(line.Debit),
                Credit = Round2(line.Credit),
                Qty = line.Qty,
                Rbp = ToNullIfWhiteSpace(line.Rbp),
                Rate = line.Rate.HasValue ? Round2(line.Rate.Value) : null,
                IsEdited = true,
                SortOrder = index
            });
        }

        await _db.SaveChangesAsync();

        var savedVoucher = await _db.JournalVouchers
            .Where(v => v.Id == voucher.Id)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .FirstAsync();

        return MapToDto(savedVoucher);
    }

    private async Task<Dictionary<int, Account>> ValidateUpdateRequestAsync(VoucherType voucherType, UpdateVoucherLedgerRequest request)
    {
        if (request.Entries.Count < 2)
            throw new InvalidOperationException("At least 2 ledger lines are required.");

        var accountIds = request.Entries.Select(e => e.AccountId).Distinct().ToList();
        var accountsById = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (accountsById.Count != accountIds.Count)
            throw new InvalidOperationException("One or more selected accounts are invalid.");

        foreach (var entry in request.Entries)
        {
            if (entry.Debit < 0 || entry.Credit < 0)
                throw new InvalidOperationException("Debit and credit cannot be negative.");

            var hasDebit = entry.Debit > 0;
            var hasCredit = entry.Credit > 0;
            if (hasDebit == hasCredit)
                throw new InvalidOperationException("Each line must have exactly one side with amount.");
        }

        var totalDebit = Round2(request.Entries.Sum(e => e.Debit));
        var totalCredit = Round2(request.Entries.Sum(e => e.Credit));
        if (totalDebit != totalCredit)
            throw new InvalidOperationException("Total debit and total credit must be equal.");

        switch (voucherType)
        {
            case VoucherType.JournalVoucher:
                ValidateJournalVoucher(request.Entries, accountsById);
                break;
            case VoucherType.ReceiptVoucher:
                ValidateReceiptVoucher(request.Entries, accountsById);
                break;
            case VoucherType.PaymentVoucher:
                ValidatePaymentVoucher(request.Entries, accountsById);
                break;
            case VoucherType.SaleVoucher:
                ValidateSaleVoucher(request.Entries, accountsById);
                break;
            case VoucherType.CartageVoucher:
                ValidateCartageVoucher(request.Entries, accountsById);
                break;
        }

        return accountsById;
    }

    private static void ValidateJournalVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        foreach (var entry in entries)
        {
            var account = accountsById[entry.AccountId];
            if (account.AccountType is AccountType.Product)
                throw new InvalidOperationException("Product accounts are not allowed in journal vouchers.");

            var isAllowed = account.AccountType is AccountType.Expense or AccountType.Party or AccountType.Account;
            if (!isAllowed)
                throw new InvalidOperationException("Only expense, party, and account types are allowed in journal vouchers.");
        }
    }

    private static void ValidateSaleVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        var productLines = entries
            .Where(e => accountsById[e.AccountId].AccountType == AccountType.Product)
            .ToList();

        if (!productLines.Any())
            throw new InvalidOperationException("Sale voucher must contain product lines.");

        foreach (var line in productLines)
        {
            if (line.Debit > 0)
                throw new InvalidOperationException("Product lines in sale voucher must be credit entries.");

            if (!line.Qty.HasValue || line.Qty <= 0)
                throw new InvalidOperationException("Product lines in sale voucher require quantity.");

            if (!string.Equals(line.Rbp, "Yes", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(line.Rbp, "No", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Product lines in sale voucher require RBP as Yes/No.");

            if (!line.Rate.HasValue || line.Rate < 0)
                throw new InvalidOperationException("Product lines in sale voucher require a valid rate.");
        }

        var hasCustomerDebit = entries.Any(e =>
        {
            if (e.Debit <= 0) return false;
            var account = accountsById[e.AccountId];
            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Customer
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        });

        if (!hasCustomerDebit)
            throw new InvalidOperationException("Sale voucher requires a debit entry for a customer party account.");
    }

    private static void ValidateCartageVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        var hasCustomerDebit = entries.Any(e =>
        {
            if (e.Debit <= 0) return false;
            var account = accountsById[e.AccountId];
            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Customer
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        });

        var hasTransporterCredit = entries.Any(e =>
        {
            if (e.Credit <= 0) return false;
            var account = accountsById[e.AccountId];
            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Transporter
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        });

        if (!hasCustomerDebit || !hasTransporterCredit)
            throw new InvalidOperationException("Cartage voucher requires customer debit and transporter credit lines.");
    }

    private static void ValidateReceiptVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        if (entries.Count(entry => entry.Credit > 0) != 1)
            throw new InvalidOperationException("Receipt voucher allows only one credit line for the customer.");

        var partyCredits = entries.Where(entry =>
        {
            if (entry.Credit <= 0) return false;
            var account = accountsById[entry.AccountId];
            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Customer
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        }).ToList();

        if (partyCredits.Count != 1)
            throw new InvalidOperationException("Receipt voucher requires exactly one customer credit line.");

        var debitLines = entries.Where(entry => entry.Debit > 0).ToList();
        if (!debitLines.Any())
            throw new InvalidOperationException("Receipt voucher requires cash or bank debit lines.");

        if (debitLines.Any(entry => accountsById[entry.AccountId].AccountType != AccountType.Account))
            throw new InvalidOperationException("Receipt voucher allows only cash and bank debit lines.");
    }

    private static void ValidatePaymentVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        if (entries.Count(entry => entry.Debit > 0) != 1)
            throw new InvalidOperationException("Payment voucher allows only one debit line for the vendor.");

        var partyDebits = entries.Where(entry =>
        {
            if (entry.Debit <= 0) return false;
            var account = accountsById[entry.AccountId];
            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Vendor
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        }).ToList();

        if (partyDebits.Count != 1)
            throw new InvalidOperationException("Payment voucher requires exactly one vendor debit line.");

        var creditLines = entries.Where(entry => entry.Credit > 0).ToList();
        if (!creditLines.Any())
            throw new InvalidOperationException("Payment voucher requires cash or bank credit lines.");

        if (creditLines.Any(entry => accountsById[entry.AccountId].AccountType != AccountType.Account))
            throw new InvalidOperationException("Payment voucher allows only cash and bank credit lines.");
    }

    private static bool ComputeRatesAdded(
        VoucherType voucherType,
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById,
        bool existingValue)
    {
        return voucherType switch
        {
            VoucherType.JournalVoucher => true,
            VoucherType.ReceiptVoucher => true,
            VoucherType.PaymentVoucher => true,
            VoucherType.CartageVoucher => true,
            VoucherType.SaleVoucher => entries
                .Where(e => accountsById[e.AccountId].AccountType == AccountType.Product)
                .All(e => (e.Rate ?? 0) > 0),
            _ => existingValue
        };
    }

    private static bool TryParseVoucherType(string? voucherType, out VoucherType parsed)
    {
        return Enum.TryParse(voucherType, true, out parsed);
    }

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? ToNullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static VoucherLedgerDto MapToDto(JournalVoucher voucher) => new()
    {
        Id = voucher.Id,
        VoucherNumber = voucher.VoucherNumber,
        VoucherType = voucher.VoucherType.ToString(),
        Date = voucher.Date,
        Description = ResolveDescription(voucher),
        VehicleNumber = voucher.VehicleNumber,
        RatesAdded = voucher.RatesAdded,
        TotalDebit = voucher.Entries.Sum(e => e.Debit),
        TotalCredit = voucher.Entries.Sum(e => e.Credit),
        Entries = voucher.Entries
            .OrderBy(e => e.SortOrder)
            .Select(e => new VoucherLedgerEntryDto
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
            })
            .ToList()
    };

    private static string? ResolveDescription(JournalVoucher voucher)
    {
        var explicitDescription = ToNullIfWhiteSpace(voucher.Description);
        if (explicitDescription != null)
            return explicitDescription;

        return voucher.VoucherType switch
        {
            VoucherType.SaleVoucher => BuildProductVoucherDescription(voucher, "Sale to"),
            VoucherType.PurchaseVoucher => BuildProductVoucherDescription(voucher, "Purchase by"),
            VoucherType.ReceiptVoucher => BuildPartyVoucherDescription(voucher, "Receipt from"),
            VoucherType.PaymentVoucher => BuildPartyVoucherDescription(voucher, "Payment to"),
            _ => null
        };
    }

    private static string? BuildProductVoucherDescription(JournalVoucher voucher, string prefix)
    {
        var partyEntry = voucher.Entries.FirstOrDefault(entry => entry.SortOrder == 0);
        var partyName = partyEntry?.Account?.Name;

        var productSummary = string.Join(", ", voucher.Entries
            .Where(entry => entry.SortOrder > 0)
            .OrderBy(entry => entry.SortOrder)
            .Select(entry => $"{entry.Account?.Name ?? $"Product {entry.AccountId}"} ({entry.Qty ?? 0})"));

        if (string.IsNullOrWhiteSpace(partyName) && string.IsNullOrWhiteSpace(productSummary))
            return null;

        if (string.IsNullOrWhiteSpace(productSummary))
            return $"{prefix} {partyName}";

        return $"{prefix} {partyName ?? "Unknown"} - {productSummary}";
    }

    private static string? BuildPartyVoucherDescription(JournalVoucher voucher, string prefix)
    {
        var partyEntry = voucher.VoucherType == VoucherType.ReceiptVoucher
            ? voucher.Entries.FirstOrDefault(entry => entry.Credit > 0)
            : voucher.Entries.FirstOrDefault(entry => entry.Debit > 0);

        var partyName = partyEntry?.Account?.Name;
        return string.IsNullOrWhiteSpace(partyName) ? null : $"{prefix} {partyName}";
    }
}
