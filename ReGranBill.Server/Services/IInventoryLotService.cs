using ReGranBill.Server.DTOs.Inventory;

namespace ReGranBill.Server.Services;

public interface IInventoryLotService
{
    Task<List<AvailableInventoryLotDto>> GetAvailableLotsForWashingAsync(int vendorId, int accountId, int? voucherId = null);
    Task<List<AvailableInventoryLotDto>> GetAvailableLotsForProductionAsync(int accountId, int? voucherId = null);
    Task<List<AvailableInventoryLotDto>> GetAvailableLotsForPurchaseReturnAsync(int vendorId, int accountId, int? voucherId = null);
    Task<Dictionary<int, decimal>> GetAvailableWeightByLotIdsAsync(IReadOnlyCollection<int> lotIds);
    Task<RawMaterialLotReportDto> GetRawMaterialLotReportAsync(RawMaterialLotReportQueryDto query);
}
