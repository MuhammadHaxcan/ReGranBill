using ReGranBill.Server.DTOs.AccountClosingReport;

namespace ReGranBill.Server.Services;

public interface IAccountClosingReportService
{
    Task<AccountClosingReportDto> GetReportAsync(DateOnly? from, DateOnly? to, int? accountId, int? historyAccountId = null);
}
