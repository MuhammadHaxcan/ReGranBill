namespace ReGranBill.Server.Entities;

public class JournalEntry
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? Qty { get; set; }
    public string? Rbp { get; set; }
    public decimal? Rate { get; set; }
    public bool IsEdited { get; set; } = false;
    public int SortOrder { get; set; }

    public JournalVoucher JournalVoucher { get; set; } = null!;
    public Account Account { get; set; } = null!;
}
