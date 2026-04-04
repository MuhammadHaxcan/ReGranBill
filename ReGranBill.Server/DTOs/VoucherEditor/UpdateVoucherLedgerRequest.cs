namespace ReGranBill.Server.DTOs.VoucherEditor;

public class UpdateVoucherLedgerRequest
{
    public string VoucherType { get; set; } = string.Empty;
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? VehicleNumber { get; set; }
    public List<UpdateVoucherLedgerEntryRequest> Entries { get; set; } = new();
}

public class UpdateVoucherLedgerEntryRequest
{
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? Qty { get; set; }
    public decimal? TotalWeightKg { get; set; }
    public string? Rbp { get; set; }
    public decimal? Rate { get; set; }
    public int SortOrder { get; set; }
}
