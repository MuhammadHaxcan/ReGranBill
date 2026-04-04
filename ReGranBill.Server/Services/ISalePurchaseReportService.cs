using ReGranBill.Server.DTOs.SalePurchaseReport;

namespace ReGranBill.Server.Services;

public interface ISalePurchaseReportService
{
    Task<SalePurchaseReportDto> GetReportAsync(DateOnly? from, DateOnly? to, string? type, int? productId);
}
