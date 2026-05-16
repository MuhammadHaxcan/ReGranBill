using ReGranBill.Server.DTOs.ProductionVouchers;

namespace ReGranBill.Server.Services;

public interface IProductionVoucherService
{
    Task<List<ProductionVoucherListDto>> GetAllAsync();
    Task<ProductionVoucherDto?> GetByIdAsync(int id);
    Task<ProductionVoucherDto?> GetByNumberAsync(string voucherNumber);
    Task<string> GetNextNumberAsync();
    Task<ProductionVoucherDto> CreateAsync(CreateProductionVoucherRequest request, int userId);
    Task<ProductionVoucherDto?> UpdateAsync(int id, CreateProductionVoucherRequest request);
    Task<(bool Success, string? Error)> SoftDeleteAsync(int id);
}
