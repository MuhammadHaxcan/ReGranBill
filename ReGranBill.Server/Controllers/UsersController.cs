using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReGranBill.Server.DTOs.Users;
using ReGranBill.Server.Exceptions;
using ReGranBill.Server.Services;

namespace ReGranBill.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;

    public UsersController(IUserManagementService userManagementService) => _userManagementService = userManagementService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _userManagementService.GetAllAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var result = await _userManagementService.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var actingUserId = GetActingUserId();
        var result = await _userManagementService.UpdateAsync(id, request, actingUserId);
        if (result == null) return NotFound();
        return Ok(result);
    }

    private int GetActingUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claimValue, out var userId))
        {
            throw new RequestValidationException("Invalid authenticated user context.");
        }

        return userId;
    }
}
