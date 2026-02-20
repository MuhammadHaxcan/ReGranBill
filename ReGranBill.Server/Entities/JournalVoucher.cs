using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class JournalVoucher
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public VoucherType VoucherType { get; set; } = VoucherType.SaleVoucher;
    public int? DcId { get; set; }
    public string? Description { get; set; }
    public bool RatesAdded { get; set; } = false;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DeliveryChallan? DeliveryChallan { get; set; }
    public User Creator { get; set; } = null!;
    public ICollection<JournalEntry> Entries { get; set; } = new List<JournalEntry>();
}
