using System.Net;
using System.Text.RegularExpressions;
using RaceDay.Web.Tests.Infrastructure;

namespace RaceDay.Web.Tests.Integration;

/// <summary>Organiser and Participant journeys through the MVC pages, with all data going through the API.</summary>
public class WorkflowTests : WebTestBase
{
    public WorkflowTests(RaceDayWebFactory factory) : base(factory) { }

    [Fact]
    public async Task HomePage_ListsUpcomingEventsFromTheApi()
    {
        var (_, _, name) = await CreateEventThroughApiAsync();

        var html = await NewBrowser().GetStringAsync("/");

        Assert.Contains(name, html);
    }

    [Fact]
    public async Task Organiser_CreatesAnEvent_AndItAppearsForEveryone()
    {
        var organiser = await RegisterThroughMvcAsync("Organiser");
        var name = $"Valley Run {Guid.NewGuid():N}"[..18];

        var response = await SubmitFormAsync(organiser, "/Organiser/Events/Create", "/Organiser/Events/Create", new Dictionary<string, string>
        {
            ["EventName"] = name,
            ["Description"] = "Hilly 21km through the valley.",
            ["EventDate"] = DateTime.Today.AddDays(40).ToString("yyyy-MM-dd"),
            ["Location"] = "Stellenbosch, Western Cape",
            ["DistanceKm"] = "21.1",
            ["EventType"] = "Run"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Organiser/Categories", response.Headers.Location!.OriginalString);

        var home = await NewBrowser().GetStringAsync("/");
        Assert.Contains(name, home);
        var dashboard = await organiser.GetStringAsync("/Organiser/Dashboard");
        Assert.Contains(name, dashboard);
    }

    [Fact]
    public async Task CreateEvent_WithMissingFields_ShowsValidationMessages()
    {
        var organiser = await RegisterThroughMvcAsync("Organiser");

        var response = await SubmitFormAsync(organiser, "/Organiser/Events/Create", "/Organiser/Events/Create", new Dictionary<string, string>
        {
            ["EventName"] = "",
            ["Location"] = "",
            ["DistanceKm"] = "0",
            ["EventType"] = ""
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Enter the event name.", html);
        Assert.Contains("Enter where the event takes place.", html);
        Assert.Contains("The distance must be between 0.1 and 9999.99 km.", html);
    }

    [Fact]
    public async Task Participant_EnrolsFromTheEventPage_AndSeesItAsConfirmed()
    {
        var (eventId, categoryId, name) = await CreateEventThroughApiAsync();
        var participant = await RegisterThroughMvcAsync("Participant");

        var response = await SubmitFormAsync(participant, $"/Events/Details/{eventId}", "/Participant/Enrolments/Enrol",
            new Dictionary<string, string> { ["eventId"] = eventId.ToString(), ["categoryId"] = categoryId.ToString() });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var mine = await participant.GetStringAsync("/Participant/Enrolments");
        Assert.Contains(name, mine);
        Assert.Contains("status-badge--Confirmed", mine);
        Assert.Matches(new Regex(@"class=""bib"">RD\d{5}<"), mine);
    }

    [Fact]
    public async Task TwoParticipants_NeverShareAnApiSession()
    {
        var (eventId, categoryId, name) = await CreateEventThroughApiAsync();
        var first = await RegisterThroughMvcAsync("Participant");
        var second = await RegisterThroughMvcAsync("Participant");

        await SubmitFormAsync(first, $"/Events/Details/{eventId}", "/Participant/Enrolments/Enrol",
            new Dictionary<string, string> { ["eventId"] = eventId.ToString(), ["categoryId"] = categoryId.ToString() });

        Assert.Contains(name, await first.GetStringAsync("/Participant/Enrolments"));
        Assert.DoesNotContain(name, await second.GetStringAsync("/Participant/Enrolments"));
    }

    [Fact]
    public async Task OrganiserCannotOpenAnotherOrganisersEvent()
    {
        var (eventId, _, _) = await CreateEventThroughApiAsync();
        var otherOrganiser = await RegisterThroughMvcAsync("Organiser");

        var response = await otherOrganiser.GetAsync($"/Organiser/Events/Edit/{eventId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task UnknownEvent_ShowsTheNotFoundPage()
    {
        var response = await NewBrowser().GetAsync("/Events/Details/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("find that page", await response.Content.ReadAsStringAsync());
    }
}

/// <summary>The MVC app must not crash or show raw errors when the API cannot be reached.</summary>
public class ApiUnavailableTests : IDisposable
{
    private readonly RaceDayWebFactory _factory = new() { ApiHandlerOverride = () => new FailingHandler() };

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ApiDown_ShowsTheServiceUnavailablePage()
    {
        var response = await _factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("reach RaceDay right now", html);
        Assert.DoesNotContain("HttpRequestException", html);
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
