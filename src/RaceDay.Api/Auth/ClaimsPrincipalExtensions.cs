using System.Security.Claims;

namespace RaceDay.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The logged-in user's UserID. Only call on endpoints protected by [Authorize].</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("No authenticated user."));
}
