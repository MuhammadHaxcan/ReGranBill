using Microsoft.EntityFrameworkCore;
using ReGranBill.Server.Data;
using ReGranBill.Server.DTOs.Auth;

namespace ReGranBill.Server.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<LoginAttemptResult> LoginAsync(LoginRequest request)
    {
        var normalizedUsername = (request.Username ?? string.Empty).Trim();
        var normalizedLookup = NormalizeUsernameKey(normalizedUsername);
        var user = await _db.Users
            .Include(u => u.Role)
                .ThenInclude(r => r.Pages)
            .FirstOrDefaultAsync(u =>
                u.IsActive &&
                u.Username.ToUpper() == normalizedLookup);
        if (user == null)
        {
            return new LoginAttemptResult
            {
                ErrorMessage = "User Name is wrong"
            };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new LoginAttemptResult
            {
                ErrorMessage = "Password is wrong"
            };
        }

        var pages = user.Role.Pages.Select(p => p.PageKey).ToList();

        return new LoginAttemptResult
        {
            Response = new LoginResponse
            {
                Token = _tokenService.GenerateToken(user, user.Role, pages),
                Username = user.Username,
                FullName = user.FullName,
                RoleId = user.Role.Id,
                RoleName = user.Role.Name,
                IsAdmin = user.Role.IsAdmin,
                Pages = pages
            }
        };
    }

    private static string NormalizeUsernameKey(string username) =>
        username.ToUpperInvariant();
}
