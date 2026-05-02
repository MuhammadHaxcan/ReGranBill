using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ReGranBill.Server.Controllers;

internal static class ControllerAuthExtensions
{
    public static bool TryGetAuthenticatedUserId(this ControllerBase controller, out int userId) =>
        int.TryParse(controller.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public static IActionResult InvalidUserSession(this ControllerBase controller) =>
        controller.Unauthorized(new { statusCode = StatusCodes.Status401Unauthorized, message = "Invalid user session." });
}
