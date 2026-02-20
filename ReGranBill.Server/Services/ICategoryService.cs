using ReGranBill.Server.DTOs.Categories;

namespace ReGranBill.Server.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request);
    Task<CategoryDto?> UpdateAsync(int id, CreateCategoryRequest request);
    Task<bool> DeleteAsync(int id);
}
