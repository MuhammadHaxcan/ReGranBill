namespace ReGranBill.Server.Entities;

public class DcLine
{
    public int Id { get; set; }
    public int DcId { get; set; }
    public int ProductId { get; set; }
    public string Rbp { get; set; } = "Yes"; // Yes | No
    public int Qty { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }

    public DeliveryChallan DeliveryChallan { get; set; } = null!;
    public Account Product { get; set; } = null!;
}
