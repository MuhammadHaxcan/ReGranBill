using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public VoucherType VoucherType { get; set; }
    public string VoucherLineKey { get; set; } = string.Empty;
    public InventoryTransactionType TransactionType { get; set; }
    public int ProductAccountId { get; set; }
    public int LotId { get; set; }
    public int? QtyDelta { get; set; }
    public decimal WeightKgDelta { get; set; }
    public decimal Rate { get; set; }
    public decimal ValueDelta { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public JournalVoucher Voucher { get; set; } = null!;
    public Account ProductAccount { get; set; } = null!;
    public InventoryLot Lot { get; set; } = null!;
}
