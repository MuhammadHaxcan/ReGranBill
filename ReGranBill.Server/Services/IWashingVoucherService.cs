using ReGranBill.Server.DTOs.WashingVouchers;

namespace ReGranBill.Server.Services;

public interface IWashingVoucherService
{
    Task<List<WashingVoucherListDto>> GetAllAsync();
    Task<WashingVoucherDto?> GetByIdAsync(int id);
    Task<WashingVoucherDto?> GetByNumberAsync(string voucherNumber);
    Task<string> GetNextNumberAsync();
    Task<WashingVoucherDto> CreateAsync(CreateWashingVoucherRequest request, int userId);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
    Task<LatestUnwashedRateDto?> GetLatestUnwashedRateAsync(int vendorId, int unwashedAccountId);
}
