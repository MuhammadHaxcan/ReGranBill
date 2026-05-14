namespace ReGranBill.Server.DTOs.ProductionVouchers;

public class CreateProductionVoucherRequest
{
    public DateOnly Date { get; set; }
    public string? LotNumber { get; set; }
    public string? Description { get; set; }
    public int? FormulationId { get; set; }
    public List<ProductionLineRequest> Inputs { get; set; } = new();
    public List<ProductionLineRequest> Outputs { get; set; } = new();
    public List<ProductionLineRequest> Byproducts { get; set; } = new();
    public ProductionShortageRequest? Shortage { get; set; }
}

public class ProductionLineRequest
{
    public int AccountId { get; set; }
    public int Qty { get; set; }
    public decimal WeightKg { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    /// Inputs: vendor (Party) whose material this line consumes. Null for outputs/byproducts.
    public int? VendorId { get; set; }

    /// Inputs: cost rate per kg pulled from latest PurchaseVoucher for (vendor, account); user-editable.
    public decimal? Rate { get; set; }
}

public class ProductionShortageRequest
{
    public int AccountId { get; set; }
    public decimal WeightKg { get; set; }
}
