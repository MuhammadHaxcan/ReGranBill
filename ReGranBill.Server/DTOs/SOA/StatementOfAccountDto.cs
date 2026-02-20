namespace ReGranBill.Server.DTOs.SOA;

public class StatementOfAccountDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetBalance { get; set; }
    public List<StatementEntryDto> Entries { get; set; } = new();
}

public class StatementEntryDto
{
    public int EntryId { get; set; }
    public DateTime Date { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}
