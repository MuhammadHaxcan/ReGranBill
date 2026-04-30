using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Accounts;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;

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
                        (a.PartyDetail.PartyRole == PartyRole.Transporter || a.PartyDetail.PartyRole == PartyRole.Both))
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<AccountDto>> GetProductsAsync()
    {
        var accounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Where(a => a.AccountType == AccountType.Product || a.AccountType == AccountType.RawMaterial)
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

    public async Task<List<AccountDto>> GetByCategoryAsync(int categoryId)
    {
        var accounts = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .Where(a => a.CategoryId == categoryId)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return accounts.Select(MapToDto).ToList();
    }

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request)
    {
        var accountName = ValidateName(request.Name);
        await ValidateCategoryAsync(request.CategoryId);
        await EnsureUniqueNameAsync(accountName);

        var accountType = ParseAccountType(request.AccountType);
        var partyRole = ParsePartyRole(accountType, request.PartyRole);
        var account = new Account
        {
            Name = accountName,
            CategoryId = request.CategoryId,
            AccountType = accountType
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        await CreateDetailRow(account.Id, request, accountType, partyRole);
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

        var accountName = ValidateName(request.Name);
        await ValidateCategoryAsync(request.CategoryId);
        await EnsureUniqueNameAsync(accountName, id);

        var accountType = ParseAccountType(request.AccountType);
        var partyRole = ParsePartyRole(accountType, request.PartyRole);
        account.Name = accountName;
        account.CategoryId = request.CategoryId;
        account.AccountType = accountType;
        await _db.SaveChangesAsync();

        await CreateDetailRow(account.Id, request, accountType, partyRole);
        return await GetByIdAsync(account.Id);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int id)
    {
        var account = await _db.Accounts
            .Include(a => a.ProductDetail)
            .Include(a => a.BankDetail)
            .Include(a => a.PartyDetail)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null) return (false, null);

        var hasEntries = await _db.JournalEntries.AnyAsync(je => je.AccountId == id);
        if (hasEntries)
            return (false, $"Cannot delete \"{account.Name}\" because it is used in voucher entries.");

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private async Task CreateDetailRow(int accountId, CreateAccountRequest request, AccountType accountType, PartyRole? partyRole)
    {
        switch (accountType)
        {
            case AccountType.Product:
            case AccountType.RawMaterial:
                _db.ProductDetails.Add(new ProductDetail
                {
                    AccountId = accountId,
                    Packing = request.Packing ?? "",
                    PackingWeightKg = request.PackingWeightKg ?? 0
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
                    PartyRole = partyRole ?? PartyRole.Customer,
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
        Packing = a.ProductDetail?.Packing,
        PackingWeightKg = a.ProductDetail?.PackingWeightKg,
        AccountNumber = a.BankDetail?.AccountNumber,
        BankName = a.BankDetail?.BankName,
        PartyRole = a.PartyDetail?.PartyRole.ToString(),
        ContactPerson = a.PartyDetail?.ContactPerson,
        Phone = a.PartyDetail?.Phone,
        City = a.PartyDetail?.City,
        Address = a.PartyDetail?.Address
    };

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new RequestValidationException("Account name is required.");
        }

        return trimmed;
    }

    private async Task ValidateCategoryAsync(int categoryId)
    {
        if (categoryId <= 0 || !await _db.Categories.AnyAsync(c => c.Id == categoryId))
        {
            throw new RequestValidationException("Select a valid category.");
        }
    }

    private async Task EnsureUniqueNameAsync(string name, int? existingId = null)
    {
        var duplicateExists = await _db.Accounts.AnyAsync(a =>
            a.Name == name && (!existingId.HasValue || a.Id != existingId.Value));

        if (duplicateExists)
        {
            throw new ConflictException($"An account named \"{name}\" already exists.");
        }
    }

    private static AccountType ParseAccountType(string? accountType)
    {
        if (!Enum.TryParse(accountType, true, out AccountType parsed))
        {
            throw new RequestValidationException("Select a valid account type.");
        }

        return parsed;
    }

    private static PartyRole? ParsePartyRole(AccountType accountType, string? partyRole)
    {
        if (accountType != AccountType.Party)
        {
            return null;
        }

        if (!Enum.TryParse(partyRole, true, out PartyRole parsed))
        {
            throw new RequestValidationException("Select a valid party role.");
        }

        return parsed;
    }
}
