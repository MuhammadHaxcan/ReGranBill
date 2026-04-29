using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.Auth;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result.Response == null)
            return Unauthorized(new { message = result.ErrorMessage ?? "Login failed" });
        return Ok(result.Response);
    }
}
