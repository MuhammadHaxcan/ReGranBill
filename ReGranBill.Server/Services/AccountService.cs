using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Accounts;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class AccountService : IAccountService
{
    private readonly AppDbContext _db;

    public AccountService(AppDbContext db) => _db = db;

    public async Task<List<AccountDto>> GetAllAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetCustomersAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => a.AccountType == AccountType.Party && a.PartyDetail != null &&
                        (a.PartyDetail.PartyRole == PartyRole.Customer || a.PartyDetail.PartyRole == PartyRole.Both))
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetVendorsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => a.AccountType == AccountType.Party && a.PartyDetail != null &&
                        (a.PartyDetail.PartyRole == PartyRole.Vendor || a.PartyDetail.PartyRole == PartyRole.Both))
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetTransportersAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.PartyDetail)
            .Where(a => a.AccountType == AccountType.Party && a.PartyDetail != null &&
                        a.PartyDetail.PartyRole == PartyRole.Transporter)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetProductsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => a.AccountType == AccountType.Product)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetJournalAccountsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .Where(a => a.AccountType == AccountType.Expense
                || a.AccountType == AccountType.Party
                || a.AccountType == AccountType.Account)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request)
    {
        var accountType = Enum.Parse<AccountType>(request.AccountType);
        var account = new Account
        {
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            AccountType = accountType
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        await CreateDetailRow(account.Id, request, accountType);
        return await GetByIdAsync(account.Id);
    }

    public async Task<AccountDto?> UpdateAsync(int id, CreateAccountRequest request)
    {
        var account = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return null;

        // Remove old detail rows if type changed
        if (account.ProductDetail != null) _db.ProductDetails.Remove(account.ProductDetail);
        if (account.BankDetail != null) _db.BankDetails.Remove(account.BankDetail);
        if (account.PartyDetail != null) _db.PartyDetails.Remove(account.PartyDetail);

        var accountType = Enum.Parse<AccountType>(request.AccountType);
        account.Name = request.Name.Trim();
        account.CategoryId = request.CategoryId;
        account.AccountType = accountType;
        await _db.SaveChangesAsync();

        await CreateDetailRow(account.Id, request, accountType);
        return await GetByIdAsync(account.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var account = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return false;
        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task CreateDetailRow(int accountId, CreateAccountRequest request, AccountType accountType)
    {
        switch (accountType)
        {
            case AccountType.Product:
                _db.ProductDetails.Add(new ProductDetail
                {
                    AccountId = accountId,
                    Packing = request.Packing ?? "",
                    PackingWeightKg = request.PackingWeightKg ?? 0,
                    Unit = request.Unit ?? "kg"
                });
                break;
            case AccountType.Account:
                _db.BankDetails.Add(new BankDetail
                {
                    AccountId = accountId,
                    AccountNumber = request.AccountNumber,
                    BankName = request.BankName
                });
                break;
            case AccountType.Party:
                _db.PartyDetails.Add(new PartyDetail
                {
                    AccountId = accountId,
                    PartyRole = Enum.Parse<PartyRole>(request.PartyRole ?? "Customer"),
                    ContactPerson = request.ContactPerson,
                    Phone = request.Phone,
                    City = request.City,
                    Address = request.Address
                });
                break;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<AccountDto> GetByIdAsync(int id)
    {
        var account = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .FirstAsync(a => a.Id == id);
        return MapToDto(account);
    }

    private static AccountDto MapToDto(Account a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        CategoryId = a.CategoryId,
        AccountType = a.AccountType.ToString(),
        Packing = a.ProductDetail != null ? a.ProductDetail.Packing : null,
        PackingWeightKg = a.ProductDetail != null ? a.ProductDetail.PackingWeightKg : null,
        Unit = a.ProductDetail != null ? a.ProductDetail.Unit : null,
        AccountNumber = a.BankDetail != null ? a.BankDetail.AccountNumber : null,
        BankName = a.BankDetail != null ? a.BankDetail.BankName : null,
        PartyRole = a.PartyDetail != null ? a.PartyDetail.PartyRole.ToString() : null,
        ContactPerson = a.PartyDetail != null ? a.PartyDetail.ContactPerson : null,
        Phone = a.PartyDetail != null ? a.PartyDetail.Phone : null,
        City = a.PartyDetail != null ? a.PartyDetail.City : null,
        Address = a.PartyDetail != null ? a.PartyDetail.Address : null
    };
}
