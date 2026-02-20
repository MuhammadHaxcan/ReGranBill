using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class DeliveryChallan
{
    public int Id { get; set; }
    public string DcNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int CustomerId { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public VoucherType VoucherType { get; set; } = VoucherType.SaleVoucher;
    public ChallanStatus Status { get; set; } = ChallanStatus.Draft;
    public bool RatesAdded { get; set; } = false;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Account Customer { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public ICollection<DcLine> Lines { get; set; } = new List<DcLine>();
    public DcCartage? Cartage { get; set; }
    public ICollection<JournalVoucher> JournalVouchers { get; set; } = new List<JournalVoucher>();
}
