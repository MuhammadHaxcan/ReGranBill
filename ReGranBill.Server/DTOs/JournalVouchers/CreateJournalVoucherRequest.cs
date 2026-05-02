namespace ReGranBill.Server.DTOs.JournalVouchers;

public class CreateJournalVoucherRequest
{
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public List<CreateJournalVoucherLineRequest> Entries { get; set; } = new();
}

public class CreateJournalVoucherLineRequest
{
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int SortOrder { get; set; }
}
