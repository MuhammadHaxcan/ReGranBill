using ReGranBill.Server.DTOs.CustomerLedger;

namespace ReGranBill.Server.Services;

public interface ICustomerLedgerService
{
    Task<CustomerLedgerDto?> GetLedgerAsync(int accountId, DateOnly? fromDate, DateOnly? toDate);
    Task<List<CustomerLedgerDto>> GetAllLedgersAsync(string partyType, DateOnly? fromDate, DateOnly? toDate);
}
