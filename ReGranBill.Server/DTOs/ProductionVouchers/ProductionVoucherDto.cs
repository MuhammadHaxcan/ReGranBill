namespace ReGranBill.Server.DTOs.ProductionVouchers;

public class ProductionVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? LotNumber { get; set; }
    public string? Description { get; set; }
    public string VoucherType { get; set; } = string.Empty;
    public int? FormulationId { get; set; }
    public List<ProductionLineDto> Inputs { get; set; } = new();
    public List<ProductionLineDto> Outputs { get; set; } = new();
    public List<ProductionLineDto> Byproducts { get; set; } = new();
    public ProductionShortageDto? Shortage { get; set; }
    public decimal TotalInputKg { get; set; }
    public decimal TotalOutputKg { get; set; }
    public decimal TotalByproductKg { get; set; }
    public decimal ShortageKg { get; set; }
    public decimal TotalInputCost { get; set; }
    public decimal DerivedOutputRate { get; set; }
}

public class ProductionLineDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int? SelectedLotId { get; set; }
    public string? SelectedLotNumber { get; set; }
    public string? AccountName { get; set; }
    public string? Packing { get; set; }
    public decimal? PackingWeightKg { get; set; }
    public int Qty { get; set; }
    public decimal WeightKg { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public decimal? Rate { get; set; }
}

public class ProductionShortageDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? Rate { get; set; }
}

public class ProductionVoucherListDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public string? LotNumber { get; set; }
    public decimal TotalInputKg { get; set; }
    public decimal TotalOutputKg { get; set; }
    public decimal TotalByproductKg { get; set; }
    public decimal ShortageKg { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LatestPurchaseRateDto
{
    public int LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public decimal AvailableWeightKg { get; set; }
    public decimal Rate { get; set; }
    public string SourceVoucherNumber { get; set; } = string.Empty;
    public DateOnly SourceDate { get; set; }
}
