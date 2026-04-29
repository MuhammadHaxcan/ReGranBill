using ReGranBill.Server.DTOs.Auth;

namespace ReGranBill.Server.Services;

public interface IAuthService
{
    Task<LoginAttemptResult> LoginAsync(LoginRequest request);
}
