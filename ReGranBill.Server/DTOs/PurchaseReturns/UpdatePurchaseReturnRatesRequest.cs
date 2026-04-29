namespace ReGranBill.Server.DTOs.PurchaseReturns;

public class UpdatePurchaseReturnRatesRequest
{
    public List<PurchaseReturnRateLineRequest> Lines { get; set; } = new();
}

public class PurchaseReturnRateLineRequest
{
    public int EntryId { get; set; }
    public decimal Rate { get; set; }
}