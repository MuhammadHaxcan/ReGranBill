using ReGranBill.Server.DTOs.DeliveryChallans;

namespace ReGranBill.Server.Services;

public interface IPurchaseVoucherService
{
    Task<List<DeliveryChallanDto>> GetAllAsync();
    Task<DeliveryChallanDto?> GetByIdAsync(int id);
    Task<string> GetNextNumberAsync();
    Task<DeliveryChallanDto> CreateAsync(CreateDcRequest request, int userId);
    Task<DeliveryChallanDto?> UpdateAsync(int id, CreateDcRequest request);
    Task<bool> UpdateRatesAsync(int id, UpdateDcRatesRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
}
