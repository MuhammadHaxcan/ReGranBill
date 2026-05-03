using ReGranBill.Server.DTOs.Roles;

namespace ReGranBill.Server.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(int id);
    Task<RoleDto> CreateAsync(CreateRoleRequest request);
    Task<RoleDto?> UpdateAsync(int id, UpdateRoleRequest request);
    Task<(bool ok, string? error)> DeleteAsync(int id);
}
