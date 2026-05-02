namespace ReGranBill.Server.DTOs.CashVouchers;

public class CashVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public bool RatesAdded { get; set; }
    public int PartyAccountId { get; set; }
    public string PartyAccountName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public List<CashVoucherLineDto> Lines { get; set; } = new();
}

public class CashVoucherLineDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public bool IsEdited { get; set; }
    public int SortOrder { get; set; }
}
