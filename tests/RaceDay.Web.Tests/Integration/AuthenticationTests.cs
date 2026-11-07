using System.Net;
using RaceDay.Web.Tests.Infrastructure;

namespace RaceDay.Web.Tests.Integration;

/// <summary>Registration, login, logout and role-based access through the MVC app and the real API.</summary>
public class AuthenticationTests : WebTestBase
{
    public AuthenticationTests(RaceDayWebFactory factory) : base(factory) { }

    [Theory]
    [InlineData("/Organiser/Dashboard")]
    [InlineData("/Organiser/Events/Create")]
    [InlineData("/Participant/Enrolments")]
    [InlineData("/Participant/Results")]
    [InlineData("/Account/Profile")]
    public async Task ProtectedPage_SendsVisitorsToLogin(string url)
    {
        var response = await NewBrowser().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.OriginalString);
        Assert.Contains("returnUrl", response.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task RegisteringAsOrganiser_LogsIn_AndOpensTheDashboard()
    {
        var browser = NewBrowser();
        var response = await SubmitFormAsync(browser, "/Account/Register", "/Account/Register", new Dictionary<string, string>
        {
            ["Role"] = "Organiser",
            ["FullName"] = "Dashboard Check",
            ["Email"] = UniqueEmail("organiser"),
            ["Password"] = Password,
            ["ConfirmPassword"] = Password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Organiser", response.Headers.Location!.OriginalString);

        var dashboard = await browser.GetAsync("/Organiser/Dashboard");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var html = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("Hi, Dashboard", html);
        Assert.Contains("My events", html);
    }

    [Fact]
    public async Task Participant_IsSentToAccessDenied_OnOrganiserPages()
    {
        var participant = await RegisterThroughMvcAsync("Participant");

        var response = await participant.GetAsync("/Organiser/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Organiser_IsSentToAccessDenied_OnParticipantPages()
    {
        var organiser = await RegisterThroughMvcAsync("Organiser");

        var response = await organiser.GetAsync("/Participant/Results");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Login_WithAWrongPassword_ShowsAFriendlyMessage()
    {
        var email = UniqueEmail("participant");
        await RegisterThroughMvcAsync("Participant", email);
        var browser = NewBrowser();

        var response = await SubmitFormAsync(browser, "/Account/Login", "/Account/Login",
            new Dictionary<string, string> { ["Email"] = email, ["Password"] = "not-the-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid email or password.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WithInvalidDetails_StaysOnTheFormWithMessages()
    {
        var response = await SubmitFormAsync(NewBrowser(), "/Account/Register", "/Account/Register", new Dictionary<string, string>
        {
            ["Role"] = "Participant",
            ["FullName"] = "A",
            ["Email"] = "not-an-email",
            ["Password"] = "short",
            ["ConfirmPassword"] = "different"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter a valid email address.", html);
        Assert.Contains("at least 8 characters", html);
        Assert.Contains("The passwords do not match.", html);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var participant = await RegisterThroughMvcAsync("Participant");
        Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync("/Participant/Enrolments")).StatusCode);

        var logout = await SubmitFormAsync(participant, "/", "/Account/Logout", new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var afterwards = await participant.GetAsync("/Participant/Enrolments");
        Assert.Equal(HttpStatusCode.Redirect, afterwards.StatusCode);
        Assert.StartsWith("/Account/Login", afterwards.Headers.Location!.OriginalString);
    }
}
