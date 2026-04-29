namespace ReGranBill.Server.DTOs.SaleReturns;

public class UpdateSaleReturnRatesRequest
{
    public List<SaleReturnRateLineRequest> Lines { get; set; } = new();
}

public class SaleReturnRateLineRequest
{
    public int EntryId { get; set; }
    public decimal Rate { get; set; }
}