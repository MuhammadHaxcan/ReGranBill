using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.DTOs.SaleReturns;

namespace ReGranBill.Server.Services;

public interface ISaleReturnService
{
    Task<List<SaleReturnDto>> GetAllAsync();
    Task<SaleReturnDto?> GetByIdAsync(int id);
    Task<string> GetNextNumberAsync();
    Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds);
    Task<SaleReturnDto> CreateAsync(CreateSaleReturnRequest request, int userId);
    Task<SaleReturnDto?> UpdateAsync(int id, CreateSaleReturnRequest request);
    Task<bool> UpdateRatesAsync(int id, UpdateSaleReturnRatesRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
}