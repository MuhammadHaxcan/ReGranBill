using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ReGranBill.Server.Authorization;

/// <summary>
/// Authorization filter that allows access if the user has access to ANY of the
/// supplied page keys. Used for shared read endpoints where multiple page roles
/// should be able to read the data (e.g. formulation list is visible to both
/// formulations managers and production-voucher operators).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAnyPageAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _pageKeys;

    public RequireAnyPageAttribute(params string[] pageKeys)
    {
        _pageKeys = pageKeys;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var raw = context.HttpContext.User.FindFirst(RequirePageAttribute.PagesClaimType)?.Value ?? string.Empty;
        if (raw == RequirePageAttribute.AdminPagesClaimValue)
            return;

        var allowed = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var key in _pageKeys)
        {
            if (allowed.Contains(key, StringComparer.Ordinal))
                return;
        }

        context.Result = new ForbidResult();
    }
}
