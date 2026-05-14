using ReGranBill.Server.DTOs.DeliveryChallans;
using ReGranBill.Server.DTOs.Common;

namespace ReGranBill.Server.Services;

public interface IDeliveryChallanService
{
    Task<List<DeliveryChallanDto>> GetAllAsync();
    Task<DeliveryChallanDto?> GetByIdAsync(int id);
    Task<DeliveryChallanDto?> GetByNumberAsync(string dcNumber);
    Task<string> GetNextNumberAsync();
    Task<DeliveryChallanDto> CreateAsync(CreateDcRequest request, int userId);
    Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request);
    Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
    Task<List<LatestProductRateDto>> GetLatestRatesAsync(IReadOnlyCollection<int> productIds);
}
