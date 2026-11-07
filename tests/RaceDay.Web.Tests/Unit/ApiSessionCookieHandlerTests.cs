using RaceDay.Web.Services;

namespace RaceDay.Web.Tests.Unit;

public class ApiSessionCookieHandlerTests
{
    [Fact]
    public void ReadCookie_FindsTheApiSessionCookie()
    {
        var headers = new[]
        {
            ".AspNetCore.Antiforgery.x=abc; path=/",
            ".RaceDay.Session=CfDJ8value123; path=/; samesite=lax; httponly"
        };

        Assert.Equal("CfDJ8value123", ApiSessionCookieHandler.ReadCookie(headers));
    }

    [Fact]
    public void ReadCookie_IgnoresOtherCookies()
    {
        Assert.Null(ApiSessionCookieHandler.ReadCookie(new[] { ".RaceDay.Web.Session=mvc; path=/" }));
        Assert.Null(ApiSessionCookieHandler.ReadCookie(Array.Empty<string>()));
    }
}
