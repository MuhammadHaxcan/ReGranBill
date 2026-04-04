namespace ReGranBill.Server.DTOs.MasterReport;

public class MasterReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? CategoryName { get; set; }
    public string? AccountName { get; set; }
    public int TotalEntries { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetBalance { get; set; }
    public List<MasterReportEntryDto> Entries { get; set; } = new();
}

public class MasterReportEntryDto
{
    public int EntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}
