namespace ReGranBill.Server.DTOs.Accounts;

public class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string AccountType { get; set; } = string.Empty; // enum name as string

    // Product-specific
    public string? Packing { get; set; }
    public decimal? PackingWeightKg { get; set; }

    // Account/Bank-specific
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }

    // Party-specific
    public string? PartyRole { get; set; } // enum name as string
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }

    // Optional legacy link retained for older UnwashedMaterial records.
    public int? WashedAccountId { get; set; }
}
