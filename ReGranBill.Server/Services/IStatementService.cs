using ReGranBill.Server.DTOs.SOA;

namespace ReGranBill.Server.Services;

public interface IStatementService
{
    Task<StatementOfAccountDto?> GetStatementAsync(int accountId, DateOnly? from, DateOnly? to);
}
