namespace RaceDay.Web.Services;

/// <summary>
/// Sends the current user's API session cookie with every request to the API, and remembers the cookie when the
/// API issues one (at login). The HttpClient itself keeps no cookies, so users can never share an API session.
/// </summary>
public class ApiSessionCookieHandler : DelegatingHandler
{
    public const string ApiCookieName = ".RaceDay.Session";

    private readonly UserSession _session;

    public ApiSessionCookieHandler(UserSession session) => _session = session;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookie = _session.ApiCookie;
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            var value = ReadCookie(setCookies);
            if (value is not null) _session.ApiCookie = value;
        }
        return response;
    }

    /// <summary>Finds the API session cookie's value in a list of Set-Cookie headers.</summary>
    public static string? ReadCookie(IEnumerable<string> setCookieHeaders)
    {
        foreach (var header in setCookieHeaders)
        {
            if (!header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;
            var value = header[(ApiCookieName.Length + 1)..].Split(';', 2)[0];
            return string.IsNullOrEmpty(value) ? null : value;
        }
        return null;
    }
}
