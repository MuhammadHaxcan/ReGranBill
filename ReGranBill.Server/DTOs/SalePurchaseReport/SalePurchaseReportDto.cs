namespace ReGranBill.Server.DTOs.SalePurchaseReport;

public class SalePurchaseReportDto
{
    public int TotalRows { get; set; }
    public int TotalSaleRows { get; set; }
    public int TotalPurchaseRows { get; set; }
    public int TotalPackedBags { get; set; }
    public decimal TotalWeightKg { get; set; }
    public List<SalePurchaseReportRowDto> Rows { get; set; } = new();
}

public class SalePurchaseReportRowDto
{
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Packing { get; set; }
    public string? Unit { get; set; }
    public string Rbp { get; set; } = "Yes";
    public int Qty { get; set; }
    public decimal LooseWeightKg { get; set; }
    public decimal PackingWeightKg { get; set; }
    public decimal TotalWeightKg { get; set; }
    public string DisplayQuantity { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string? TransporterName { get; set; }
    public string GroupLabel { get; set; } = string.Empty;
    public DateTime GroupSortDate { get; set; }
}
