using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Users;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Enums;
using ReGranBill.Server.Exceptions;

namespace ReGranBill.Server.Services;

public class UserManagementService : IUserManagementService
{
    private readonly AppDbContext _db;

    public UserManagementService(AppDbContext db) => _db = db;

    public async Task<List<UserDto>> GetAllAsync()
    {
        var users = await _db.Users
            .OrderBy(u => u.Username)
            .ToListAsync();

        return users.Select(MapToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        var username = ValidateUsername(request.Username);
        var fullName = ValidateFullName(request.FullName);
        var password = ValidatePassword(request.Password, required: true);
        var role = ParseRole(request.Role);

        await EnsureUniqueUsernameAsync(username);

        var user = new User
        {
            Username = username,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password!),
            Role = role,
            IsActive = true
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return MapToDto(user);
    }

    public async Task<UserDto?> UpdateAsync(int id, UpdateUserRequest request, int actingUserId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        var username = ValidateUsername(request.Username);
        var fullName = ValidateFullName(request.FullName);
        var role = ParseRole(request.Role);
        var password = ValidatePassword(request.Password, required: false);

        await EnsureUniqueUsernameAsync(username, id);
        await EnsureAdminSafeguardsAsync(user, role, request.IsActive, actingUserId);

        user.Username = username;
        user.FullName = fullName;
        user.Role = role;
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }

        await _db.SaveChangesAsync();
        return MapToDto(user);
    }

    private async Task EnsureUniqueUsernameAsync(string username, int? existingId = null)
    {
        var duplicateExists = await _db.Users.AnyAsync(u =>
            u.Username == username && (!existingId.HasValue || u.Id != existingId.Value));

        if (duplicateExists)
        {
            throw new ConflictException($"A user with username \"{username}\" already exists.");
        }
    }

    private async Task EnsureAdminSafeguardsAsync(User user, UserRole updatedRole, bool updatedIsActive, int actingUserId)
    {
        var removesAdminAccess = user.Role == UserRole.Admin && user.IsActive &&
            (updatedRole != UserRole.Admin || !updatedIsActive);

        if (!removesAdminAccess)
        {
            return;
        }

        var otherActiveAdmins = await _db.Users.CountAsync(u =>
            u.Id != user.Id && u.IsActive && u.Role == UserRole.Admin);

        if (otherActiveAdmins == 0)
        {
            throw new RequestValidationException("At least one active admin user is required.");
        }

        if (user.Id == actingUserId && !updatedIsActive)
        {
            throw new RequestValidationException("You cannot deactivate your own user account.");
        }
    }

    private static string ValidateUsername(string? username)
    {
        var trimmed = username?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new RequestValidationException("Username is required.");
        }

        if (trimmed.Length < 3)
        {
            throw new RequestValidationException("Username must be at least 3 characters.");
        }

        return trimmed;
    }

    private static string ValidateFullName(string? fullName)
    {
        var trimmed = fullName?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new RequestValidationException("Full name is required.");
        }

        return trimmed;
    }

    private static string? ValidatePassword(string? password, bool required)
    {
        var trimmed = password?.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            if (required)
            {
                throw new RequestValidationException("Password is required.");
            }

            return null;
        }

        if (trimmed.Length < 6)
        {
            throw new RequestValidationException("Password must be at least 6 characters.");
        }

        return trimmed;
    }

    private static UserRole ParseRole(string? role)
    {
        if (!Enum.TryParse(role, true, out UserRole parsed))
        {
            throw new RequestValidationException("Select a valid user role.");
        }

        return parsed;
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Role = user.Role.ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt
    };
}
