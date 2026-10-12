namespace RaceDay.Api.Auth;

/// <summary>Keys under which the logged-in user's state is stored in the server-side session.</summary>
public static class SessionKeys
{
    public const string UserId = "RaceDay.UserId";
    public const string Role = "RaceDay.Role";
    public const string FullName = "RaceDay.FullName";
    public const string Email = "RaceDay.Email";
}
