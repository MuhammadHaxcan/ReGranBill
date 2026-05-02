namespace ReGranBill.Server.DTOs.Common;

public class LatestProductRateDto
{
    public int ProductId { get; set; }
    public decimal Rate { get; set; }
    public string SourceVoucherNumber { get; set; } = string.Empty;
    public DateOnly SourceDate { get; set; }
}
