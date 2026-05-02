namespace ReGranBill.Server.DTOs.MasterReport;

public class MasterReportDto
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? CategoryName { get; set; }
    public string? AccountName { get; set; }
    public int TotalEntries { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetBalance { get; set; }
    public List<MasterReportAccountSummaryDto> AccountSummaries { get; set; } = new();
    public List<MasterReportEntryDto> Entries { get; set; } = new();
}

public class MasterReportEntryDto
{
    public int EntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class MasterReportAccountSummaryDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
}
