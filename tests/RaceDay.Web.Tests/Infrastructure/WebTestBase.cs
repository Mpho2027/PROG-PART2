using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay.Web.Tests.Infrastructure;

/// <summary>Helpers for driving the MVC app like a browser: forms with antiforgery tokens and a cookie jar per user.</summary>
public abstract class WebTestBase : IClassFixture<RaceDayWebFactory>
{
    protected const string Password = "Racing2026!";

    protected WebTestBase(RaceDayWebFactory factory) => Factory = factory;

    protected RaceDayWebFactory Factory { get; }

    /// <summary>A browser with its own cookies (so its own MVC session).</summary>
    protected HttpClient NewBrowser() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    protected static string UniqueEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.co.za";

    /// <summary>Loads the page with the form, then posts the form fields with the page's antiforgery token.</summary>
    protected static async Task<HttpResponseMessage> SubmitFormAsync(HttpClient browser, string formPage, string action, IDictionary<string, string> fields)
    {
        var page = await browser.GetStringAsync(formPage);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(token.Success, $"No antiforgery token found on {formPage}");

        var body = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token.Groups[1].Value };
        return await browser.PostAsync(action, new FormUrlEncodedContent(body));
    }

    protected async Task<HttpClient> RegisterThroughMvcAsync(string role, string? email = null)
    {
        var browser = NewBrowser();
        var response = await SubmitFormAsync(browser, "/Account/Register", "/Account/Register", new Dictionary<string, string>
        {
            ["Role"] = role,
            ["FullName"] = $"Test {role}",
            ["Email"] = email ?? UniqueEmail(role.ToLowerInvariant()),
            ["Password"] = Password,
            ["ConfirmPassword"] = Password
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    /// <summary>Creates an upcoming event with one category directly through the API (test arrangement).</summary>
    protected async Task<(int EventId, int CategoryId, string EventName)> CreateEventThroughApiAsync(string? name = null)
    {
        var api = Factory.Api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var email = UniqueEmail("organiser");
        await api.PostAsJsonAsync("/api/auth/register", new { fullName = "Test Organiser", email, password = Password, role = "Organiser" });
        await api.PostAsJsonAsync("/api/auth/login", new { email, password = Password });

        name ??= $"Test Race {Guid.NewGuid():N}"[..20];
        var created = await api.PostAsJsonAsync("/api/events", new
        {
            eventName = name,
            description = "A test event.",
            eventDate = DateTime.Today.AddDays(20).ToString("yyyy-MM-dd"),
            location = "Pretoria, Gauteng",
            distanceKm = 10,
            eventType = "Run"
        });
        var ev = await created.Content.ReadFromJsonAsync<IdHolder>();
        var category = await api.PostAsJsonAsync($"/api/events/{ev!.EventId}/categories", new { categoryName = "10km", distanceKm = 10, maxParticipants = 50, entryFee = 100 });
        var cat = await category.Content.ReadFromJsonAsync<IdHolder>();
        return (ev.EventId, cat!.CategoryId, name);
    }

    private sealed record IdHolder(int EventId, int CategoryId);
}
