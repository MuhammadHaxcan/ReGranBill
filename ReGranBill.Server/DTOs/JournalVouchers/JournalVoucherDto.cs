namespace ReGranBill.Server.DTOs.JournalVouchers;

public class JournalVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RatesAdded { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalVoucherEntryDto> Entries { get; set; } = new();
}

public class JournalVoucherEntryDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public bool IsEdited { get; set; }
    public int SortOrder { get; set; }
}
