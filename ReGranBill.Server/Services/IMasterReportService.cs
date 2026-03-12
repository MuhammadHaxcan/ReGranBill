using ReGranBill.Server.DTOs.MasterReport;

namespace ReGranBill.Server.Services;

public interface IMasterReportService
{
    Task<MasterReportDto> GetReportAsync(DateOnly? from, DateOnly? to, int? categoryId, int? accountId);
}
