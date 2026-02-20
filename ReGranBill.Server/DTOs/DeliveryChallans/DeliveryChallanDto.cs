namespace ReGranBill.Server.DTOs.DeliveryChallans;

public class DeliveryChallanDto
{
    public int Id { get; set; }
    public string DcNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public string VoucherType { get; set; } = "SaleVoucher";
    public bool RatesAdded { get; set; }
    public List<DcLineDto> Lines { get; set; } = new();
    public DcCartageDto? Cartage { get; set; }
    public List<JournalVoucherSummaryDto> JournalVouchers { get; set; } = new();
}

public class DcLineDto
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

public class DcCartageDto
{
    public int TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? City { get; set; }
    public decimal Amount { get; set; }
}

public class JournalVoucherSummaryDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public bool RatesAdded { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalEntryDto> Entries { get; set; } = new();
}

public class JournalEntryDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? Qty { get; set; }
    public string? Rbp { get; set; }
    public decimal? Rate { get; set; }
    public int SortOrder { get; set; }
}
