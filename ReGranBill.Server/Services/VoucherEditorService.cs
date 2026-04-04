using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.VoucherEditor;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class VoucherEditorService : IVoucherEditorService
{
    private readonly AppDbContext _db;

    public VoucherEditorService(AppDbContext db) => _db = db;

    public async Task<VoucherLedgerDto?> FindByTypeAndNumberAsync(string voucherType, string voucherNumber)
    {
        if (!TryParseVoucherType(voucherType, out var parsedType))
            throw new RequestValidationException("Invalid voucher type.");

        var number = voucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number))
            throw new RequestValidationException("Voucher number is required.");

        var voucher = await _db.JournalVouchers
            .Where(v => v.VoucherType == parsedType && v.VoucherNumber == number)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .FirstOrDefaultAsync();

        return voucher == null ? null : MapToDto(voucher);
    }

    public async Task<VoucherLedgerDto?> UpdateAsync(UpdateVoucherLedgerRequest request)
    {
        if (!TryParseVoucherType(request.VoucherType, out var voucherType))
            throw new RequestValidationException("Invalid voucher type.");

        var voucherNumber = request.VoucherNumber?.Trim();
        if (string.IsNullOrWhiteSpace(voucherNumber))
            throw new RequestValidationException("Voucher number is required.");

        var voucher = await _db.JournalVouchers
            .Where(v => v.VoucherType == voucherType && v.VoucherNumber == voucherNumber)
            .Include(v => v.Entries)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        var accountsById = await ValidateUpdateRequestAsync(voucherType, request);
        await ValidateLinkedVoucherConsistencyAsync(voucher, voucherType, request);

        voucher.Date = VoucherHelpers.NormalizeToUtc(request.Date);
        voucher.Description = VoucherHelpers.ToNullIfWhiteSpace(request.Description);
        voucher.VehicleNumber = SupportsVehicleNumber(voucherType)
            ? VoucherHelpers.ToNullIfWhiteSpace(request.VehicleNumber)
            : null;
        voucher.UpdatedAt = DateTime.UtcNow;
        voucher.RatesAdded = ComputeRatesAdded(voucherType, request.Entries, accountsById, voucher.RatesAdded);

        _db.JournalEntries.RemoveRange(voucher.Entries);
        voucher.Entries.Clear();

        foreach (var (line, index) in request.Entries.OrderBy(e => e.SortOrder).Select((line, index) => (line, index)))
        {
            var account = accountsById[line.AccountId];
            var isInventoryLine = IsInventoryAccount(account);

            voucher.Entries.Add(new JournalEntry
            {
                AccountId = line.AccountId,
                Description = VoucherHelpers.ToNullIfWhiteSpace(line.Description),
                Debit = VoucherHelpers.Round2(line.Debit),
                Credit = VoucherHelpers.Round2(line.Credit),
                Qty = SupportsInventoryMeta(voucherType) && isInventoryLine ? line.Qty : null,
                ActualWeightKg = voucherType == VoucherType.PurchaseVoucher && isInventoryLine && line.TotalWeightKg.HasValue
                    ? VoucherHelpers.Round2(line.TotalWeightKg.Value)
                    : null,
                Rbp = voucherType == VoucherType.PurchaseVoucher && isInventoryLine
                    ? "Yes"
                    : (voucherType == VoucherType.SaleVoucher && isInventoryLine
                        ? VoucherHelpers.ToNullIfWhiteSpace(line.Rbp)
                        : null),
                Rate = SupportsInventoryMeta(voucherType) && isInventoryLine && line.Rate.HasValue
                    ? VoucherHelpers.Round2(line.Rate.Value)
                    : null,
                IsEdited = true,
                SortOrder = index
            });
        }

        await SynchronizeLinkedCartageVoucherAsync(voucher, voucherType, accountsById);

        await _db.SaveChangesAsync();

        var savedVoucher = await _db.JournalVouchers
            .Where(v => v.Id == voucher.Id)
            .Include(v => v.Entries).ThenInclude(e => e.Account)
            .FirstAsync();

        return MapToDto(savedVoucher);
    }

    private async Task ValidateLinkedVoucherConsistencyAsync(
        JournalVoucher voucher,
        VoucherType voucherType,
        UpdateVoucherLedgerRequest request)
    {
        if (voucherType != VoucherType.CartageVoucher)
            return;

        var cartageReference = await _db.JournalVoucherReferences
            .Where(reference => reference.ReferenceVoucherId == voucher.Id)
            .Include(reference => reference.MainVoucher).ThenInclude(mainVoucher => mainVoucher.Entries)
            .FirstOrDefaultAsync();

        var saleVoucher = cartageReference?.MainVoucher;
        if (saleVoucher == null)
            return;

        var saleCustomerLine = saleVoucher.Entries.FirstOrDefault(entry => entry.Debit > 0);
        if (saleCustomerLine == null)
            return;

        var cartageCustomerLines = request.Entries.Where(entry => entry.Debit > 0).ToList();
        if (cartageCustomerLines.Count != 1)
            throw new RequestValidationException("Cartage voucher must contain exactly one customer debit line.");

        if (cartageCustomerLines[0].AccountId != saleCustomerLine.AccountId)
            throw new RequestValidationException("Cartage voucher customer must match the linked sale voucher customer.");

        if (VoucherHelpers.NormalizeToUtc(request.Date).Date != saleVoucher.Date.Date)
            throw new RequestValidationException("Cartage voucher date must match the linked sale voucher date.");
    }

    private async Task<Dictionary<int, Account>> ValidateUpdateRequestAsync(VoucherType voucherType, UpdateVoucherLedgerRequest request)
    {
        if (request.Entries.Count < 2)
            throw new RequestValidationException("At least 2 ledger lines are required.");

        var accountIds = request.Entries.Select(e => e.AccountId).Distinct().ToList();
        var accountsById = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Include(a => a.ProductDetail)
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        if (accountsById.Count != accountIds.Count)
            throw new RequestValidationException("One or more selected accounts are invalid.");

        foreach (var entry in request.Entries)
        {
            if (entry.Debit < 0 || entry.Credit < 0)
                throw new RequestValidationException("Debit and credit cannot be negative.");

            var hasDebit = entry.Debit > 0;
            var hasCredit = entry.Credit > 0;
            if (hasDebit == hasCredit)
                throw new RequestValidationException("Each line must have exactly one side with amount.");
        }

        var totalDebit = VoucherHelpers.Round2(request.Entries.Sum(e => e.Debit));
        var totalCredit = VoucherHelpers.Round2(request.Entries.Sum(e => e.Credit));
        if (totalDebit != totalCredit)
            throw new RequestValidationException("Total debit and total credit must be equal.");

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
            case VoucherType.PurchaseVoucher:
                ValidatePurchaseVoucher(request.Entries, accountsById);
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
            if (IsInventoryAccount(account))
                throw new RequestValidationException("Inventory accounts are not allowed in journal vouchers.");

            var isAllowed = account.AccountType is AccountType.Expense or AccountType.Party or AccountType.Account;
            if (!isAllowed)
                throw new RequestValidationException("Only expense, party, and account types are allowed in journal vouchers.");
        }
    }

    private static void ValidateSaleVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        var productLines = entries
            .Where(e => IsInventoryAccount(accountsById[e.AccountId]))
            .ToList();

        if (!productLines.Any())
            throw new RequestValidationException("Sale voucher must contain inventory lines.");

        var expectedTotal = 0m;

        foreach (var line in productLines)
        {
            if (line.Debit > 0)
                throw new RequestValidationException("Inventory lines in sale voucher must be credit entries.");

            if (!line.Qty.HasValue || line.Qty <= 0)
                throw new RequestValidationException("Inventory lines in sale voucher require quantity.");

            if (!string.Equals(line.Rbp, "Yes", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(line.Rbp, "No", StringComparison.OrdinalIgnoreCase))
                throw new RequestValidationException("Inventory lines in sale voucher require RBP as Yes/No.");

            if (!line.Rate.HasValue || line.Rate < 0)
                throw new RequestValidationException("Inventory lines in sale voucher require a valid rate.");

            var productAccount = accountsById[line.AccountId];
            var productDetail = productAccount.ProductDetail;
            if (productDetail == null)
                throw new RequestValidationException($"{productAccount.Name} is missing inventory packing details.");

            var expectedAmount = CalculateSaleLineAmount(line, productDetail.PackingWeightKg);
            var actualAmount = VoucherHelpers.Round2(line.Credit);
            if (actualAmount != expectedAmount)
                throw new RequestValidationException(
                    $"{productAccount.Name} amount must be {expectedAmount:0.00} based on qty, RBP, packing weight, and rate.");

            expectedTotal += expectedAmount;
        }

        var debitLines = entries.Where(e => e.Debit > 0).ToList();
        if (debitLines.Count != 1)
            throw new RequestValidationException("Sale voucher requires exactly one customer debit line.");

        var customerLine = debitLines[0];
        var customerAccount = accountsById[customerLine.AccountId];
        var isCustomerDebit = customerAccount.AccountType == AccountType.Party
            && customerAccount.PartyDetail != null
            && (customerAccount.PartyDetail.PartyRole == PartyRole.Customer
                || customerAccount.PartyDetail.PartyRole == PartyRole.Both);

        if (!isCustomerDebit)
            throw new RequestValidationException("Sale voucher requires the debit line to be a customer party account.");

        var customerDebit = VoucherHelpers.Round2(customerLine.Debit);
        var roundedExpectedTotal = VoucherHelpers.Round2(expectedTotal);
        if (customerDebit != roundedExpectedTotal)
            throw new RequestValidationException(
                $"Customer debit must equal total product amount of {roundedExpectedTotal:0.00}.");

        if (entries.Count != productLines.Count + 1)
            throw new RequestValidationException("Sale voucher allows only one customer debit line and inventory credit lines.");

        if (!ReferenceEquals(entries[0], customerLine))
            throw new RequestValidationException("Customer debit line must remain the first line in the sale voucher.");

        var nonProductCredits = entries
            .Where(e => e.Credit > 0 && !IsInventoryAccount(accountsById[e.AccountId]))
            .ToList();

        if (nonProductCredits.Count > 0)
            throw new RequestValidationException("Sale voucher credit lines must be inventory accounts only.");
    }

    private static void ValidatePurchaseVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        var productLines = entries
            .Where(e => IsInventoryAccount(accountsById[e.AccountId]))
            .ToList();

        if (!productLines.Any())
            throw new RequestValidationException("Purchase voucher must contain inventory lines.");

        var expectedTotal = 0m;

        foreach (var line in productLines)
        {
            if (line.Credit > 0)
                throw new RequestValidationException("Inventory lines in purchase voucher must be debit entries.");

            if (!line.Qty.HasValue || line.Qty <= 0)
                throw new RequestValidationException("Inventory lines in purchase voucher require bags quantity.");

            if (!line.TotalWeightKg.HasValue || line.TotalWeightKg <= 0)
                throw new RequestValidationException("Inventory lines in purchase voucher require total weight in kg.");

            if (!line.Rate.HasValue || line.Rate < 0)
                throw new RequestValidationException("Inventory lines in purchase voucher require a valid rate.");

            var expectedAmount = CalculatePurchaseLineAmount(line.TotalWeightKg.Value, line.Rate.Value);
            var actualAmount = VoucherHelpers.Round2(line.Debit);
            if (actualAmount != expectedAmount)
                throw new RequestValidationException(
                    $"{accountsById[line.AccountId].Name} debit must be {expectedAmount:0.00} based on total kg and rate.");

            expectedTotal += expectedAmount;
        }

        var creditLines = entries.Where(e => e.Credit > 0).ToList();
        if (creditLines.Count != 1)
            throw new RequestValidationException("Purchase voucher requires exactly one vendor credit line.");

        var vendorLine = creditLines[0];
        var vendorAccount = accountsById[vendorLine.AccountId];
        var isVendorCredit = vendorAccount.AccountType == AccountType.Party
            && vendorAccount.PartyDetail != null
            && (vendorAccount.PartyDetail.PartyRole == PartyRole.Vendor
                || vendorAccount.PartyDetail.PartyRole == PartyRole.Both);

        if (!isVendorCredit)
            throw new RequestValidationException("Purchase voucher requires the credit line to be a vendor party account.");

        var vendorCredit = VoucherHelpers.Round2(vendorLine.Credit);
        var roundedExpectedTotal = VoucherHelpers.Round2(expectedTotal);
        if (vendorCredit != roundedExpectedTotal)
            throw new RequestValidationException(
                $"Vendor credit must equal total product amount of {roundedExpectedTotal:0.00}.");

        if (entries.Count != productLines.Count + 1)
            throw new RequestValidationException("Purchase voucher allows only one vendor credit line and inventory debit lines.");

        if (!ReferenceEquals(entries[0], vendorLine))
            throw new RequestValidationException("Vendor credit line must remain the first line in the purchase voucher.");

        var nonProductDebits = entries
            .Where(e => e.Debit > 0 && !IsInventoryAccount(accountsById[e.AccountId]))
            .ToList();

        if (nonProductDebits.Count > 0)
            throw new RequestValidationException("Purchase voucher debit lines must be inventory accounts only.");
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
            throw new RequestValidationException("Cartage voucher requires customer debit and transporter credit lines.");
    }

    private async Task SynchronizeLinkedCartageVoucherAsync(
        JournalVoucher voucher,
        VoucherType voucherType,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        if (voucherType != VoucherType.SaleVoucher)
            return;

        var cartageReference = await _db.JournalVoucherReferences
            .Where(reference => reference.MainVoucherId == voucher.Id)
            .Include(reference => reference.ReferenceVoucher).ThenInclude(referenceVoucher => referenceVoucher.Entries)
            .FirstOrDefaultAsync();

        var cartageVoucher = cartageReference?.ReferenceVoucher;
        if (cartageVoucher == null)
            return;

        var customerLine = voucher.Entries.FirstOrDefault(entry =>
        {
            if (entry.Debit <= 0) return false;
            if (!accountsById.TryGetValue(entry.AccountId, out var account)) return false;

            return account.AccountType == AccountType.Party
                && account.PartyDetail != null
                && (account.PartyDetail.PartyRole == PartyRole.Customer
                    || account.PartyDetail.PartyRole == PartyRole.Both);
        });

        if (customerLine == null)
            return;

        cartageVoucher.Date = voucher.Date;
        cartageVoucher.Description = $"Cartage entries for {voucher.VoucherNumber}";
        cartageVoucher.UpdatedAt = DateTime.UtcNow;

        var cartageCustomerLine = cartageVoucher.Entries.FirstOrDefault(entry => entry.Debit > 0);
        if (cartageCustomerLine != null)
        {
            cartageCustomerLine.AccountId = customerLine.AccountId;
            cartageCustomerLine.Description = $"Cartage charge - {voucher.VoucherNumber}";
            cartageCustomerLine.IsEdited = true;
        }

        var transporterLine = cartageVoucher.Entries.FirstOrDefault(entry => entry.Credit > 0);
        if (transporterLine != null)
        {
            transporterLine.Description = $"Cartage for {voucher.VoucherNumber}";
            transporterLine.IsEdited = true;
        }
    }

    private static void ValidateReceiptVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        if (entries.Count(entry => entry.Credit > 0) != 1)
            throw new RequestValidationException("Receipt voucher allows only one credit line for the customer.");

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
            throw new RequestValidationException("Receipt voucher requires exactly one customer credit line.");

        var debitLines = entries.Where(entry => entry.Debit > 0).ToList();
        if (!debitLines.Any())
            throw new RequestValidationException("Receipt voucher requires cash or bank debit lines.");

        if (debitLines.Any(entry => accountsById[entry.AccountId].AccountType != AccountType.Account))
            throw new RequestValidationException("Receipt voucher allows only cash and bank debit lines.");
    }

    private static void ValidatePaymentVoucher(
        List<UpdateVoucherLedgerEntryRequest> entries,
        IReadOnlyDictionary<int, Account> accountsById)
    {
        if (entries.Count(entry => entry.Debit > 0) != 1)
            throw new RequestValidationException("Payment voucher allows only one debit line for the vendor.");

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
            throw new RequestValidationException("Payment voucher requires exactly one vendor debit line.");

        var creditLines = entries.Where(entry => entry.Credit > 0).ToList();
        if (!creditLines.Any())
            throw new RequestValidationException("Payment voucher requires cash or bank credit lines.");

        if (creditLines.Any(entry => accountsById[entry.AccountId].AccountType != AccountType.Account))
            throw new RequestValidationException("Payment voucher allows only cash and bank credit lines.");
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
                .Where(e => IsInventoryAccount(accountsById[e.AccountId]))
                .All(e => (e.Rate ?? 0) > 0),
            VoucherType.PurchaseVoucher => entries
                .Where(e => IsInventoryAccount(accountsById[e.AccountId]))
                .All(e => (e.Rate ?? 0) > 0),
            _ => existingValue
        };
    }

    private static bool TryParseVoucherType(string? voucherType, out VoucherType parsed)
    {
        return Enum.TryParse(voucherType, true, out parsed);
    }

    private static decimal CalculateSaleLineAmount(UpdateVoucherLedgerEntryRequest line, decimal packingWeightKg)
    {
        var qty = line.Qty ?? 0;
        var rate = line.Rate ?? 0;
        var usesPackedWeight = string.Equals(line.Rbp, "Yes", StringComparison.OrdinalIgnoreCase);
        var amount = usesPackedWeight
            ? packingWeightKg * qty * rate
            : qty * rate;

        return VoucherHelpers.Round2(amount);
    }

    private static decimal CalculatePurchaseLineAmount(decimal totalWeightKg, decimal rate) =>
        VoucherHelpers.Round2(totalWeightKg * rate);

    private static bool IsInventoryAccount(Account account) =>
        VoucherHelpers.IsInventoryAccount(account);

    private static bool SupportsVehicleNumber(VoucherType voucherType) =>
        voucherType is VoucherType.SaleVoucher or VoucherType.PurchaseVoucher;

    private static bool SupportsInventoryMeta(VoucherType voucherType) =>
        voucherType is VoucherType.SaleVoucher or VoucherType.PurchaseVoucher;

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
                TotalWeightKg = e.ActualWeightKg,
                Rbp = e.Rbp,
                Rate = e.Rate,
                IsEdited = e.IsEdited,
                SortOrder = e.SortOrder
            })
            .ToList()
    };

    private static string? ResolveDescription(JournalVoucher voucher)
    {
        var explicitDescription = VoucherHelpers.ToNullIfWhiteSpace(voucher.Description);
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
