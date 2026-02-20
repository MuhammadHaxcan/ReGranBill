namespace ReGranBill.Server.Entities;

public class BankDetail
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankName { get; set; }

    public Account Account { get; set; } = null!;
}
