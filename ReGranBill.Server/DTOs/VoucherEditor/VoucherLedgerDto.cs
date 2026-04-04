namespace ReGranBill.Server.DTOs.VoucherEditor;

public class VoucherLedgerDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? VehicleNumber { get; set; }
    public bool RatesAdded { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<VoucherLedgerEntryDto> Entries { get; set; } = new();
}

public class VoucherLedgerEntryDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? Qty { get; set; }
    public decimal? TotalWeightKg { get; set; }
    public string? Rbp { get; set; }
    public decimal? Rate { get; set; }
    public bool IsEdited { get; set; }
    public int SortOrder { get; set; }
}
