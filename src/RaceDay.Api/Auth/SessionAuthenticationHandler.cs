using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace RaceDay.Api.Auth;

/// <summary>
/// Authenticates requests from the server-side session created at login.
/// The session cookie only carries an opaque session ID; the user's ID and role live on the server.
/// Turning the session into a ClaimsPrincipal lets controllers use the standard
/// [Authorize] / [Authorize(Roles = ...)] attributes, which return 401 (not logged in)
/// or 403 (wrong role) instead of redirecting to a login page.
/// </summary>
public class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Session";

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = Context.Session;
        await session.LoadAsync();

        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);
        if (userId is null || string.IsNullOrEmpty(role))
            return AuthenticateResult.NoResult();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.Value.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, session.GetString(SessionKeys.FullName) ?? string.Empty),
            new(ClaimTypes.Email, session.GetString(SessionKeys.Email) ?? string.Empty)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new { message = "You must be logged in to access this resource." });
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Response.WriteAsJsonAsync(new { message = "Your role is not permitted to perform this action." });
    }
}
