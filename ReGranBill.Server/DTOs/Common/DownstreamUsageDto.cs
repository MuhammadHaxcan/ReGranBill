namespace ReGranBill.Server.DTOs.Common;

public class DownstreamUsageDto
{
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int LotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int? QtyDelta { get; set; }
    public decimal WeightKgDelta { get; set; }
}
