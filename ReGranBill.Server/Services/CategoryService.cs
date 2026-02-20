using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Categories;
using ReGranBill.Server.Entities;

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

    public async Task<bool> DeleteAsync(int id)
    {
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return false;
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return true;
    }
}
