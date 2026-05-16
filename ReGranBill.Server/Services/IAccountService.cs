using ReGranBill.Server.DTOs.Accounts;
using ReGranBill.Server.DTOs.Common;

namespace ReGranBill.Server.Services;

public interface IAccountService
{
    Task<List<AccountDto>> GetAllAsync();
    Task<List<AccountDto>> GetCustomersAsync();
    Task<List<AccountDto>> GetVendorsAsync();
    Task<List<AccountDto>> GetTransportersAsync();
    Task<List<AccountDto>> GetProductsAsync();
    Task<List<AccountDto>> GetJournalAccountsAsync();
    Task<List<AccountDto>> GetByCategoryAsync(int categoryId);
    Task<AccountDto> CreateAsync(CreateAccountRequest request);
    Task<AccountDto?> UpdateAsync(int id, CreateAccountRequest request);
    Task<DeleteResult> DeleteAsync(int id);
}
