using ReGranBill.Server.DTOs.MasterReport;

namespace ReGranBill.Server.Services;

public interface IMasterReportService
{
    Task<MasterReportDto> GetReportAsync(DateTime? from, DateTime? to, int? categoryId, int? accountId);
}
