using ReGranBill.Server.DTOs.Users;

namespace ReGranBill.Server.Services;

public interface IUserManagementService
{
    Task<List<UserDto>> GetAllAsync();
    Task<UserDto> CreateAsync(CreateUserRequest request);
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, int actingUserId);
}
