namespace ReGranBill.Server.DTOs.CashVouchers;

public class CreateCashVoucherRequest
{
    public DateOnly Date { get; set; }
    public int PartyAccountId { get; set; }
    public string? Description { get; set; }
    public List<CreateCashVoucherLineRequest> Lines { get; set; } = new();
}

public class CreateCashVoucherLineRequest
{
    public int AccountId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}
