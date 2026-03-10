using ReGranBill.Server.DTOs.Accounts;

namespace ReGranBill.Server.Services;

public interface IAccountService
{
    Task<List<AccountDto>> GetAllAsync();
    Task<List<AccountDto>> GetCustomersAsync();
    Task<List<AccountDto>> GetVendorsAsync();
    Task<List<AccountDto>> GetTransportersAsync();
    Task<List<AccountDto>> GetProductsAsync();
    Task<List<AccountDto>> GetJournalAccountsAsync();
    Task<AccountDto> CreateAsync(CreateAccountRequest request);
    Task<AccountDto?> UpdateAsync(int id, CreateAccountRequest request);
    Task<bool> DeleteAsync(int id);
}
