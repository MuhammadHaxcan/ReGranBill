namespace ReGranBill.Server.DTOs.AccountClosingReport;

public class AccountClosingReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? SelectedAccountId { get; set; }
    public string? SelectedAccountName { get; set; }
    public int? HistoryAccountId { get; set; }
    public string? HistoryAccountName { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalClosingBalance { get; set; }
    public List<AccountClosingSummaryRowDto> Accounts { get; set; } = new();
    public List<AccountClosingHistoryEntryDto> History { get; set; } = new();
}

public class AccountClosingSummaryRowDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class AccountClosingHistoryEntryDto
{
    public int EntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public int? Quantity { get; set; }
    public decimal? Rate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}
