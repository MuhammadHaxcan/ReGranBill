using ReGranBill.Server.DTOs.Categories;
using ReGranBill.Server.DTOs.Common;
using ReGranBill.Server.Enums;

namespace ReGranBill.Server.Services;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync();
    Task<List<CategoryDto>> GetFilteredAsync(
        IReadOnlyCollection<AccountType> accountTypes,
        IReadOnlyCollection<PartyRole>? partyRoles);
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request);
    Task<CategoryDto?> UpdateAsync(int id, CreateCategoryRequest request);
    Task<DeleteResult> DeleteAsync(int id);
}
