using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Categories;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;

    public CategoryService(AppDbContext db) => _db = db;

    public async Task<List<CategoryDto>> GetAllAsync()
    {
        return await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<List<CategoryDto>> GetFilteredAsync(
        IReadOnlyCollection<AccountType> accountTypes,
        IReadOnlyCollection<PartyRole>? partyRoles)
    {
        if (accountTypes == null || accountTypes.Count == 0)
            return await GetAllAsync();

        var accountQuery = _db.Accounts.AsQueryable()
            .Where(a => accountTypes.Contains(a.AccountType));

        if (partyRoles != null && partyRoles.Count > 0)
        {
            accountQuery = accountQuery.Where(a =>
                a.PartyDetail != null && partyRoles.Contains(a.PartyDetail.PartyRole));
        }

        var categoryIds = await accountQuery
            .Select(a => a.CategoryId)
            .Distinct()
            .ToListAsync();

        if (categoryIds.Count == 0)
            return [];

        return await _db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request)
    {
        var cat = new Category { Name = request.Name.Trim() };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return new CategoryDto { Id = cat.Id, Name = cat.Name };
    }

    public async Task<CategoryDto?> UpdateAsync(int id, CreateCategoryRequest request)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return null;
        cat.Name = request.Name.Trim();
        await _db.SaveChangesAsync();
        return new CategoryDto { Id = cat.Id, Name = cat.Name };
    }

    public async Task<DeleteResult> DeleteAsync(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return new DeleteResult(false, null, null);

        var childAccountIds = await _db.Accounts
            .Where(a => a.CategoryId == id)
            .Select(a => a.Id)
            .ToListAsync();

        if (childAccountIds.Count > 0)
        {
            var blockingVouchers = await _db.JournalEntries
                .Where(je => childAccountIds.Contains(je.AccountId))
                .Select(je => je.JournalVoucher)
                .Distinct()
                .OrderByDescending(v => v.Date).ThenByDescending(v => v.Id)
                .ToListAsync();

            var top = blockingVouchers.Take(20)
                .Select(v => new VoucherRef(v.Id, v.VoucherNumber, v.Date, v.VoucherType.ToString()))
                .ToList();

            var msg = blockingVouchers.Count > 0
                ? $"Cannot delete \"{cat.Name}\": it has {childAccountIds.Count} account(s), used in {blockingVouchers.Count} voucher(s)."
                : $"Cannot delete \"{cat.Name}\" because it has accounts associated with it.";

            return new DeleteResult(false, null, new DeleteBlockedResult(msg, top, blockingVouchers.Count));
        }

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return new DeleteResult(true, null, null);
    }
}
