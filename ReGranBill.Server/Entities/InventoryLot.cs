using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class InventoryLot
{
    public int Id { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int ProductAccountId { get; set; }
    public int? VendorAccountId { get; set; }
    public int SourceVoucherId { get; set; }
    public VoucherType SourceVoucherType { get; set; }
    public int? SourceEntryId { get; set; }
    public int? ParentLotId { get; set; }
    public int? OriginalQty { get; set; }
    public decimal OriginalWeightKg { get; set; }
    public decimal BaseRate { get; set; }
    public InventoryLotStatus Status { get; set; } = InventoryLotStatus.Open;
    public DateOnly CreatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly UpdatedAt { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public Account ProductAccount { get; set; } = null!;
    public Account? VendorAccount { get; set; }
    public JournalVoucher SourceVoucher { get; set; } = null!;
    public JournalEntry? SourceEntry { get; set; }
    public InventoryLot? ParentLot { get; set; }
    public ICollection<InventoryLot> ChildLots { get; set; } = new List<InventoryLot>();
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}
