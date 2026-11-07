using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a controller or action using the session: visitors who are not logged in are sent to the login page,
/// and logged-in users with a different role are sent to the access denied page.
/// With no roles given, any logged-in user is allowed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    public RequireRoleAttribute(params string[] roles) => Roles = roles;

    public string[] Roles { get; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<UserSession>().User;
        if (user is null)
        {
            var request = context.HttpContext.Request;
            context.Result = new RedirectToActionResult("Login", "Account",
                new { area = "", returnUrl = $"{request.PathBase}{request.Path}{request.QueryString}" });
            return;
        }

        if (Roles.Length > 0 && !Roles.Contains(user.Role))
            context.Result = new RedirectToActionResult("AccessDenied", "Account", new { area = "" });
    }
}
