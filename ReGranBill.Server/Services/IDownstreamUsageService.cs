using ReGranBill.Server.DTOs.Common;

namespace ReGranBill.Server.Services;

public interface IDownstreamUsageService
{
    Task<List<DownstreamUsageDto>> GetForPurchaseAsync(int purchaseVoucherId);
    Task<List<DownstreamUsageDto>> GetForPurchaseReturnAsync(int prVoucherId);
    Task<List<DownstreamUsageDto>> GetForWashingAsync(int washingVoucherId);
    Task<List<DownstreamUsageDto>> GetForProductionAsync(int productionVoucherId);

    Task<bool> HasAnyForPurchaseAsync(int purchaseVoucherId);
    Task<bool> HasAnyForWashingAsync(int washingVoucherId);
    Task<bool> HasAnyForProductionAsync(int productionVoucherId);
}
