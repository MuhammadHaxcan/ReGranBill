namespace ReGranBill.Server.Entities;

public class JournalVoucherReference
{
    public int Id { get; set; }
    public int MainVoucherId { get; set; }
    public int ReferenceVoucherId { get; set; }

    public JournalVoucher MainVoucher { get; set; } = null!;
    public JournalVoucher ReferenceVoucher { get; set; } = null!;
}
