using ReGranBill.Server.DTOs.DeliveryChallans;

namespace ReGranBill.Server.DTOs.PurchaseReturns;

public class PurchaseReturnDto
{
    public int Id { get; set; }
    public string PrNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public string VoucherType { get; set; } = "PurchaseReturnVoucher";
    public bool RatesAdded { get; set; }
    public List<PurchaseReturnLineDto> Lines { get; set; } = new();
    public List<JournalVoucherSummaryDto> JournalVouchers { get; set; } = new();
}

public class PurchaseReturnLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Packing { get; set; }
    public decimal PackingWeightKg { get; set; }
    public string Rbp { get; set; } = "Yes";
    public int Qty { get; set; }
    public decimal TotalWeightKg { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}