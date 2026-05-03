using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ReGranBill.Server.Authorization;

/// <summary>
/// Authorization filter that requires the current user to have access to a given page key.
/// The user's allowed pages come from the "pages" JWT claim — value "*" means admin
/// (implicit access to all pages); otherwise a comma-separated list of page keys.
/// Used in concert with [Authorize] (which validates the token); apply both:
///   [Authorize] [RequirePage("delivery-challan")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePageAttribute : Attribute, IAuthorizationFilter
{
    public const string PagesClaimType = "pages";
    public const string AdminPagesClaimValue = "*";

    private readonly string _pageKey;

    public RequirePageAttribute(string pageKey)
    {
        _pageKey = pageKey;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var raw = context.HttpContext.User.FindFirst(PagesClaimType)?.Value ?? string.Empty;
        if (raw == AdminPagesClaimValue)
        {
            return; // admin: implicit access to every page
        }

        var allowed = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (allowed.Contains(_pageKey, StringComparer.Ordinal))
        {
            return;
        }

        context.Result = new ForbidResult();
    }
}
