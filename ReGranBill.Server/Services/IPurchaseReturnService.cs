using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.PurchaseReturns;

namespace ReGranBill.Server.Services;

public interface IPurchaseReturnService
{
    Task<List<PurchaseReturnDto>> GetAllAsync();
    Task<PurchaseReturnDto?> GetByIdAsync(int id);
    Task<PurchaseReturnDto?> GetByNumberAsync(string prNumber);
    Task<string> GetNextNumberAsync();
    Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds);
    Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnRequest request, int userId);
    Task<PurchaseReturnDto?> UpdateAsync(int id, CreatePurchaseReturnRequest request);
    Task<bool> UpdateRatesAsync(int id, UpdatePurchaseReturnRatesRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
}
