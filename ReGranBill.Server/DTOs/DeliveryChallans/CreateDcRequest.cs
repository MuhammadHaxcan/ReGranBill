namespace ReGranBill.Server.DTOs.DeliveryChallans;

public class CreateDcRequest
{
    public DateOnly Date { get; set; }
    public int CustomerId { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public List<CreateDcLineRequest> Lines { get; set; } = new();
    public CreateDcCartageRequest? Cartage { get; set; }
}

public class CreateDcLineRequest
{
    public int ProductId { get; set; }
    public string Rbp { get; set; } = "Yes";
    public int Qty { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}

public class CreateDcCartageRequest
{
    public int TransporterId { get; set; }
    public decimal Amount { get; set; }
}
