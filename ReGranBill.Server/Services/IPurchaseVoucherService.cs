using ReGranBill.Server.DTOs.PurchaseVouchers;
using ReGranBill.Server.DTOs.Common;

namespace ReGranBill.Server.Services;

public interface IPurchaseVoucherService
{
    Task<List<PurchaseVoucherDto>> GetAllAsync();
    Task<PurchaseVoucherDto?> GetByIdAsync(int id);
    Task<string> GetNextNumberAsync();
    Task<PurchaseVoucherDto> CreateAsync(CreatePurchaseVoucherRequest request, int userId);
    Task<PurchaseVoucherDto?> UpdateAsync(int id, CreatePurchaseVoucherRequest request);
    Task<bool> UpdateRatesAsync(int id, UpdatePurchaseVoucherRatesRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
    Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds);
}
