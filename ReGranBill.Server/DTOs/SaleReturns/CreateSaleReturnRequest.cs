namespace ReGranBill.Server.DTOs.SaleReturns;

public class CreateSaleReturnRequest
{
    public DateTime Date { get; set; }
    public int CustomerId { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public List<CreateSaleReturnLineRequest> Lines { get; set; } = new();
}

public class CreateSaleReturnLineRequest
{
    public int ProductId { get; set; }
    public string Rbp { get; set; } = "Yes";
    public int Qty { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}