namespace ReGranBill.Server.DTOs.WashingVouchers;

public class CreateWashingVoucherRequest
{
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public int SourceVendorId { get; set; }
    public int UnwashedAccountId { get; set; }
    public int SelectedLotId { get; set; }
    public decimal InputWeightKg { get; set; }
    public decimal InputRate { get; set; }
    public decimal OutputWeightKg { get; set; }
    public List<CreateWashingVoucherOutputLineRequest> OutputLines { get; set; } = [];

    /// Allowed wastage as a percentage of input. Anything beyond is charged back to the vendor.
    /// Default 10. Must be in (0, 100].
    public decimal ThresholdPct { get; set; } = 10m;
}

public class CreateWashingVoucherOutputLineRequest
{
    public int AccountId { get; set; }
    public decimal WeightKg { get; set; }
}

public class WashingVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public int SourceVendorId { get; set; }
    public string? SourceVendorName { get; set; }
    public int UnwashedAccountId { get; set; }
    public string? UnwashedAccountName { get; set; }
    public int SelectedLotId { get; set; }
    public string? SelectedLotNumber { get; set; }
    public int? WashedAccountId { get; set; }
    public string? WashedAccountName { get; set; }
    public decimal InputWeightKg { get; set; }
    public decimal OutputWeightKg { get; set; }
    public List<WashingVoucherOutputLineDto> OutputLines { get; set; } = [];
    public decimal WastageKg { get; set; }
    public decimal WastagePct { get; set; }
    public decimal SourceRate { get; set; }
    public decimal InputCost { get; set; }
    public decimal WashedDebit { get; set; }
    public decimal WashedRate { get; set; }
    public decimal ThresholdPct { get; set; }
    public decimal ExcessWastageKg { get; set; }
    public decimal ExcessWastageValue { get; set; }
    public DateOnly CreatedAt { get; set; }
}

public class WashingVoucherListDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Description { get; set; }
    public int SourceVendorId { get; set; }
    public string? SourceVendorName { get; set; }
    public int UnwashedAccountId { get; set; }
    public string? UnwashedAccountName { get; set; }
    public int SelectedLotId { get; set; }
    public string? SelectedLotNumber { get; set; }
    public decimal InputWeightKg { get; set; }
    public decimal OutputWeightKg { get; set; }
    public int OutputLineCount { get; set; }
    public decimal WastageKg { get; set; }
    public decimal WastagePct { get; set; }
    public decimal WashedDebit { get; set; }
    public decimal WashedRate { get; set; }
    public decimal ExcessWastageKg { get; set; }
    public decimal ExcessWastageValue { get; set; }
    public DateOnly CreatedAt { get; set; }
}

public class LatestUnwashedRateDto
{
    public int LotId { get; set; }
    public int AccountId { get; set; }
    public decimal Rate { get; set; }
    public string SourceVoucherNumber { get; set; } = string.Empty;
    public DateOnly SourceDate { get; set; }
}

public class WashingVoucherOutputLineDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal Rate { get; set; }
    public decimal Debit { get; set; }
}
