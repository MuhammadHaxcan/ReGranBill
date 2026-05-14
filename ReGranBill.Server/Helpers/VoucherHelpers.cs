using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Helpers;

internal static class VoucherHelpers
{
    public static bool IsInventoryAccount(Account? account) =>
        account?.AccountType is AccountType.Product or AccountType.RawMaterial or AccountType.UnwashedMaterial;

    public static bool IsValidRbp(string? rbp) =>
        string.Equals(rbp, "Yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(rbp, "No", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeRbp(string? rbp) =>
        string.Equals(rbp, "No", StringComparison.OrdinalIgnoreCase) ? "No" : "Yes";

    public static decimal CalculateLineAmount(decimal packingWeightKg, string? rbp, int qty, decimal rate) =>
        NormalizeRbp(rbp) == "Yes"
            ? packingWeightKg * qty * rate
            : qty * rate;

    public static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string? ToNullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? ResolveDescription(
        string? requestedDescription,
        string prefix,
        string? partyName,
        IEnumerable<(int ProductId, int SortOrder, int Qty)> lines,
        IReadOnlyDictionary<int, Account> productAccounts)
    {
        var trimmed = ToNullIfWhiteSpace(requestedDescription);
        if (trimmed != null)
            return trimmed;

        var productSummary = string.Join(", ", lines
            .OrderBy(l => l.SortOrder)
            .Select(l =>
            {
                var name = productAccounts.GetValueOrDefault(l.ProductId)?.Name ?? $"Product {l.ProductId}";
                return $"{name} ({l.Qty})";
            }));

        if (string.IsNullOrWhiteSpace(productSummary))
            return partyName == null ? null : $"{prefix} {partyName}";

        return $"{prefix} {partyName ?? "Unknown"} - {productSummary}";
    }

    public static JournalVoucher BuildCartageVoucher(
        string voucherNumber,
        DateOnly date,
        string relatedVoucherNumber,
        int userId,
        int partyId,
        int transporterId,
        decimal amount,
        bool isEdited)
    {
        var jv = new JournalVoucher
        {
            VoucherNumber = voucherNumber,
            Date = date,
            VoucherType = VoucherType.CartageVoucher,
            Description = $"Cartage entries for {relatedVoucherNumber}",
            RatesAdded = true,
            CreatedBy = userId,
        };

        jv.Entries.Add(new JournalEntry
        {
            AccountId = partyId,
            Description = $"Cartage charge - {relatedVoucherNumber}",
            Debit = amount,
            Credit = 0,
            IsEdited = isEdited,
            SortOrder = 0
        });

        jv.Entries.Add(new JournalEntry
        {
            AccountId = transporterId,
            Description = $"Cartage for {relatedVoucherNumber}",
            Debit = 0,
            Credit = amount,
            IsEdited = isEdited,
            SortOrder = 1
        });

        return jv;
    }
}
