namespace ReGranBill.Server.DTOs.PurchaseReturns;

public class CreatePurchaseReturnRequest
{
    public DateTime Date { get; set; }
    public int VendorId { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public List<CreatePurchaseReturnLineRequest> Lines { get; set; } = new();
}

public class CreatePurchaseReturnLineRequest
{
    public int ProductId { get; set; }
    public int Qty { get; set; }
    public decimal TotalWeightKg { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}