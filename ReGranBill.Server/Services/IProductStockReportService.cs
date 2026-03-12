using ReGranBill.Server.DTOs.ProductStockReport;

namespace ReGranBill.Server.Services;

public interface IProductStockReportService
{
    Task<ProductStockReportDto> GetReportAsync(ProductStockReportQueryDto query);
}
