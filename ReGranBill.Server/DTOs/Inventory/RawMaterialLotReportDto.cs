namespace ReGranBill.Server.DTOs.Inventory;

public class RawMaterialLotReportQueryDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? VendorId { get; set; }
    public int? ProductId { get; set; }
    public string? LotNumber { get; set; }
    public bool OpenOnly { get; set; }
    public bool IncludeDetails { get; set; } = true;
}

public class RawMaterialLotReportDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? VendorId { get; set; }
    public int? ProductId { get; set; }
    public string? LotNumber { get; set; }
    public bool OpenOnly { get; set; }
    public List<RawMaterialLotRowDto> Lots { get; set; } = [];
    public List<RawMaterialLotMovementDto> Movements { get; set; } = [];
}

public class RawMaterialLotRowDto
{
    public int LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public string SourceVoucherNumber { get; set; } = string.Empty;
    public DateOnly SourceDate { get; set; }
    public int? OriginalQty { get; set; }
    public decimal OriginalWeightKg { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public decimal AvailableWeightKg { get; set; }
    public decimal BaseRate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class RawMaterialLotMovementDto
{
    public int LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public decimal WeightKgIn { get; set; }
    public decimal WeightKgOut { get; set; }
    public decimal RunningAvailableKg { get; set; }
    public decimal Rate { get; set; }
    public decimal ValueDelta { get; set; }
    public string? Notes { get; set; }
}
