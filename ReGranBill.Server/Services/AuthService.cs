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
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.IsActive &&
            u.Username.ToLower() == normalizedUsername.ToLower());
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

        return new LoginAttemptResult
        {
            Response = new LoginResponse
            {
                Token = _tokenService.GenerateToken(user),
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role.ToString()
            }
        };
    }
}
