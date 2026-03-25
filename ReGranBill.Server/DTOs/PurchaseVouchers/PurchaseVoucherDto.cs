namespace ReGranBill.Server.DTOs.PurchaseVouchers;

public class PurchaseVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? VehicleNumber { get; set; }
    public string? Description { get; set; }
    public string VoucherType { get; set; } = "PurchaseVoucher";
    public bool RatesAdded { get; set; }
    public List<PurchaseVoucherLineDto> Lines { get; set; } = new();
    public PurchaseVoucherCartageDto? Cartage { get; set; }
    public List<PurchaseVoucherJournalSummaryDto> JournalVouchers { get; set; } = new();
}

public class PurchaseVoucherLineDto
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

public class PurchaseVoucherCartageDto
{
    public int TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? City { get; set; }
    public decimal Amount { get; set; }
}

public class PurchaseVoucherJournalSummaryDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public bool RatesAdded { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<PurchaseVoucherJournalEntryDto> Entries { get; set; } = new();
}

public class PurchaseVoucherJournalEntryDto
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
    public bool IsEdited { get; set; }
    public int SortOrder { get; set; }
}
