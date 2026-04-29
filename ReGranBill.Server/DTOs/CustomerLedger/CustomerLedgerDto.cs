namespace ReGranBill.Server.DTOs.CustomerLedger;

public class CustomerLedgerDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PartyType { get; set; } = string.Empty;
    public bool HasOpeningBalance { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<CustomerLedgerEntryDto> Entries { get; set; } = new();
}

public class CustomerLedgerEntryDto
{
    public int EntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public string? ProductName { get; set; }
    public string? Packing { get; set; }
    public decimal? Qty { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Rate { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}
