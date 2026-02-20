using ReGranBill.Server.DTOs.SOA;

namespace ReGranBill.Server.Services;

public interface IStatementService
{
    Task<StatementOfAccountDto?> GetStatementAsync(int accountId, DateTime? from, DateTime? to);
}
