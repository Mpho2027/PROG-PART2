using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>The logged-in user as stored in the MVC session.</summary>
public sealed record SessionUser(int UserId, string FullName, string Email, string Role, bool HasProfilePicture)
{
    public bool IsOrganiser => Role == Roles.Organiser;
    public bool IsParticipant => Role == Roles.Participant;
    public string FirstName => FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? FullName;
}

/// <summary>
/// Reads and writes the logged-in user's state in the server-side MVC session. The session also holds the
/// API's session cookie, so that every API call made for this user is authenticated as them.
/// </summary>
public class UserSession
{
    private const string UserIdKey = "User.Id";
    private const string NameKey = "User.Name";
    private const string EmailKey = "User.Email";
    private const string RoleKey = "User.Role";
    private const string PhotoKey = "User.HasPhoto";
    private const string ApiCookieKey = "Api.SessionCookie";

    private readonly IHttpContextAccessor _accessor;

    public UserSession(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession? Session => _accessor.HttpContext?.Session;

    public SessionUser? User
    {
        get
        {
            var session = Session;
            var id = session?.GetInt32(UserIdKey);
            var role = session?.GetString(RoleKey);
            if (session is null || id is null || string.IsNullOrEmpty(role)) return null;
            return new SessionUser(id.Value, session.GetString(NameKey) ?? string.Empty,
                session.GetString(EmailKey) ?? string.Empty, role, session.GetInt32(PhotoKey) == 1);
        }
    }

    public bool IsLoggedIn => User is not null;

    /// <summary>Value of the API's .RaceDay.Session cookie for this user.</summary>
    public string? ApiCookie
    {
        get => Session?.GetString(ApiCookieKey);
        set
        {
            if (Session is null) return;
            if (string.IsNullOrEmpty(value)) Session.Remove(ApiCookieKey);
            else Session.SetString(ApiCookieKey, value);
        }
    }

    public void SignIn(UserProfileDto profile)
    {
        var session = Session ?? throw new InvalidOperationException("No session is available.");
        session.SetInt32(UserIdKey, profile.UserId);
        session.SetString(RoleKey, profile.Role);
        Update(profile);
    }

    /// <summary>Refreshes the display name and picture flag after a profile change.</summary>
    public void Update(UserProfileDto profile)
    {
        var session = Session;
        if (session is null) return;
        session.SetString(NameKey, profile.FullName);
        session.SetString(EmailKey, profile.Email);
        session.SetInt32(PhotoKey, profile.HasProfilePicture ? 1 : 0);
    }

    /// <summary>Forgets the user and the API session cookie.</summary>
    public void SignOut() => Session?.Clear();
}
