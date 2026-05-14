using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public AccountType AccountType { get; set; }
    public int? WashedAccountId { get; set; }

    public Category Category { get; set; } = null!;
    public ProductDetail? ProductDetail { get; set; }
    public BankDetail? BankDetail { get; set; }
    public PartyDetail? PartyDetail { get; set; }
    public Account? WashedAccount { get; set; }
}
