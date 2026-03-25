namespace ReGranBill.Server.DTOs.PurchaseVouchers;

public class UpdatePurchaseVoucherRatesRequest
{
    public List<PurchaseVoucherLineRateUpdate> Lines { get; set; } = new();
}

public class PurchaseVoucherLineRateUpdate
{
    public int EntryId { get; set; }
    public decimal Rate { get; set; }
}
