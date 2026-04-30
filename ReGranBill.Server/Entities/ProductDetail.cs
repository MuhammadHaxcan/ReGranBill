namespace ReGranBill.Server.Entities;

public class ProductDetail
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string Packing { get; set; } = string.Empty;
    public decimal PackingWeightKg { get; set; }

    public Account Account { get; set; } = null!;
}
