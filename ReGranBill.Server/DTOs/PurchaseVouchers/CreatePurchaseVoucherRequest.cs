namespace ReGranBill.Server.DTOs.PurchaseVouchers;

public class CreatePurchaseVoucherRequest
{
    public DateOnly Date { get; set; }
    public int VendorId { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public List<CreatePurchaseVoucherLineRequest> Lines { get; set; } = new();
    public CreatePurchaseVoucherCartageRequest? Cartage { get; set; }
}

public class CreatePurchaseVoucherLineRequest
{
    public int ProductId { get; set; }
    public int Qty { get; set; }
    public decimal TotalWeightKg { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}

public class CreatePurchaseVoucherCartageRequest
{
    public int TransporterId { get; set; }
    public decimal Amount { get; set; }
}
