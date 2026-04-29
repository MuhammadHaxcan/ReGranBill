using ReGranBill.Server.DTOs.DeliveryChallans;

namespace ReGranBill.Server.DTOs.SaleReturns;

public class SaleReturnDto
{
    public int Id { get; set; }
    public string SrNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public string VoucherType { get; set; } = "SaleReturnVoucher";
    public bool RatesAdded { get; set; }
    public List<SrLineDto> Lines { get; set; } = new();
    public List<JournalVoucherSummaryDto> JournalVouchers { get; set; } = new();
}

public class SrLineDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? Packing { get; set; }
    public decimal PackingWeightKg { get; set; }
    public string Rbp { get; set; } = "Yes";
    public int Qty { get; set; }
    public decimal Rate { get; set; }
    public int SortOrder { get; set; }
}