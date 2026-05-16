using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class JournalVoucher
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public VoucherType VoucherType { get; set; } = VoucherType.SaleVoucher;
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public bool RatesAdded { get; set; } = false;
    public int CreatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly UpdatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public User Creator { get; set; } = null!;
    public ICollection<JournalEntry> Entries { get; set; } = new List<JournalEntry>();
}
