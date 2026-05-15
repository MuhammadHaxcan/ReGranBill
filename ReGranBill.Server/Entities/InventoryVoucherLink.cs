using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class InventoryVoucherLink
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public VoucherType VoucherType { get; set; }
    public string VoucherLineKey { get; set; } = string.Empty;
    public int LotId { get; set; }
    public int TransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public JournalVoucher Voucher { get; set; } = null!;
    public InventoryLot Lot { get; set; } = null!;
    public InventoryTransaction Transaction { get; set; } = null!;
}
