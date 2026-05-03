using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Roles;
using ReGranBill.Server.Entities;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Helpers;

namespace ReGranBill.Server.Services;

public class RoleService : IRoleService
{
    private readonly AppDbContext _db;

    public RoleService(AppDbContext db) => _db = db;

    public async Task<List<RoleDto>> GetAllAsync()
    {
        var roles = await _db.Roles
            .Include(r => r.Pages)
            .OrderByDescending(r => r.IsAdmin)
            .ThenBy(r => r.Name)
            .ToListAsync();

        var userCounts = await _db.Users
            .GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count);

        return roles.Select(r => MapToDto(r, userCounts.GetValueOrDefault(r.Id, 0))).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(int id)
    {
        var role = await _db.Roles
            .Include(r => r.Pages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return null;

        var userCount = await _db.Users.CountAsync(u => u.RoleId == id);
        return MapToDto(role, userCount);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
    {
        var name = ValidateName(request.Name);
        var pageKeys = ValidatePageKeys(request.Pages);

        if (await _db.Roles.AnyAsync(r => r.Name.ToUpper() == name.ToUpper()))
        {
            throw new ConflictException($"A role named \"{name}\" already exists.");
        }

        var role = new Role
        {
            Name = name,
            IsSystem = false,
            IsAdmin = false,
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            UpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        foreach (var key in pageKeys)
        {
            role.Pages.Add(new RolePage { PageKey = key });
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return MapToDto(role, 0);
    }

    public async Task<RoleDto?> UpdateAsync(int id, UpdateRoleRequest request)
    {
        var role = await _db.Roles
            .Include(r => r.Pages)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return null;

        if (role.IsSystem)
        {
            throw new ConflictException("System roles cannot be modified.");
        }

        var name = ValidateName(request.Name);
        var pageKeys = ValidatePageKeys(request.Pages);

        if (await _db.Roles.AnyAsync(r => r.Id != id && r.Name.ToUpper() == name.ToUpper()))
        {
            throw new ConflictException($"A role named \"{name}\" already exists.");
        }

        role.Name = name;
        role.UpdatedAt = DateOnly.FromDateTime(DateTime.UtcNow);

        _db.RolePages.RemoveRange(role.Pages);
        role.Pages.Clear();
        foreach (var key in pageKeys)
        {
            role.Pages.Add(new RolePage { RoleId = role.Id, PageKey = key });
        }

        await _db.SaveChangesAsync();

        var userCount = await _db.Users.CountAsync(u => u.RoleId == id);
        return MapToDto(role, userCount);
    }

    public async Task<(bool ok, string? error)> DeleteAsync(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return (false, null);

        if (role.IsSystem)
        {
            return (false, "System roles cannot be deleted.");
        }

        var userCount = await _db.Users.CountAsync(u => u.RoleId == id);
        if (userCount > 0)
        {
            return (false, $"This role is assigned to {userCount} user(s). Reassign them before deleting.");
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new RequestValidationException("Role name is required.");
        }
        if (trimmed.Length < 2)
        {
            throw new RequestValidationException("Role name must be at least 2 characters.");
        }
        if (trimmed.Length > 50)
        {
            throw new RequestValidationException("Role name cannot exceed 50 characters.");
        }
        return trimmed;
    }

    private static IReadOnlyCollection<string> ValidatePageKeys(IEnumerable<string>? keys)
    {
        var distinct = (keys ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unknown = distinct.Where(k => !PageCatalog.IsKnown(k)).ToList();
        if (unknown.Count > 0)
        {
            throw new RequestValidationException(
                $"Unknown page keys: {string.Join(", ", unknown)}");
        }

        return distinct;
    }

    private static RoleDto MapToDto(Role role, int userCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        IsSystem = role.IsSystem,
        IsAdmin = role.IsAdmin,
        Pages = role.IsAdmin
            ? PageCatalog.All.Select(p => p.Key).ToList()
            : role.Pages.Select(p => p.PageKey).OrderBy(k => k).ToList(),
        UserCount = userCount
    };
}
