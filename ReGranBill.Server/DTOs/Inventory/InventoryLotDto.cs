namespace ReGranBill.Server.DTOs.Inventory;

public class AvailableInventoryLotDto
{
    public int LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int ProductAccountId { get; set; }
    public string ProductAccountName { get; set; } = string.Empty;
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string SourceVoucherNumber { get; set; } = string.Empty;
    public string SourceVoucherType { get; set; } = string.Empty;
    public DateOnly SourceDate { get; set; }
    public decimal Rate { get; set; }
    public int? OriginalQty { get; set; }
    public decimal OriginalWeightKg { get; set; }
    public decimal AvailableWeightKg { get; set; }
}
