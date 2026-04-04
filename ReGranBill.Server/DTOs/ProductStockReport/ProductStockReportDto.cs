namespace ReGranBill.Server.DTOs.ProductStockReport;

public class ProductStockReportQueryDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? CategoryId { get; set; }
    public int? ProductId { get; set; }
    public bool IncludeDetails { get; set; } = false;
}

public class ProductStockReportDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? CategoryId { get; set; }
    public int? ProductId { get; set; }
    public bool IncludeDetails { get; set; }
    public ProductStockTotalsDto Totals { get; set; } = new();
    public List<ProductStockRowDto> Products { get; set; } = new();
    public List<ProductStockMovementDto> Movements { get; set; } = new();
    public List<ProductStockAnomalyDto> Anomalies { get; set; } = new();
}

public class ProductStockTotalsDto
{
    public int ProductCount { get; set; }
    public int AnomalyCount { get; set; }
    public ProductStockMetricDto Opening { get; set; } = new();
    public ProductStockMetricDto Inward { get; set; } = new();
    public ProductStockMetricDto Outward { get; set; } = new();
    public ProductStockMetricDto Closing { get; set; } = new();
}

public class ProductStockRowDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Packing { get; set; }
    public decimal? PackingWeightKg { get; set; }
    public int AnomalyCount { get; set; }
    public ProductStockMetricDto Opening { get; set; } = new();
    public ProductStockMetricDto Inward { get; set; } = new();
    public ProductStockMetricDto Outward { get; set; } = new();
    public ProductStockMetricDto Closing { get; set; } = new();
}

public class ProductStockMetricDto
{
    public decimal Bags { get; set; }
    public decimal Kg { get; set; }
    public decimal Value { get; set; }
}

public class ProductStockMovementDto
{
    public int EntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? Qty { get; set; }
    public string? Rbp { get; set; }
    public decimal? Rate { get; set; }
    public decimal WeightKg { get; set; }
    public decimal Value { get; set; }
    public string Direction { get; set; } = string.Empty;
    public bool IsEdited { get; set; }
    public string? AnomalyNote { get; set; }
}

public class ProductStockAnomalyDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
}
