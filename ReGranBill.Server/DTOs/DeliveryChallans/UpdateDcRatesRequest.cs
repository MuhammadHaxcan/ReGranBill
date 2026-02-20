namespace ReGranBill.Server.DTOs.DeliveryChallans;

public class UpdateDcRatesRequest
{
    public List<LineRateUpdate> Lines { get; set; } = new();
}

public class LineRateUpdate
{
    public int EntryId { get; set; }
    public decimal Rate { get; set; }
}
