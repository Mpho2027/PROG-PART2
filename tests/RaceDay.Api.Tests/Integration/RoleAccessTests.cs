using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>
/// Role-based access across the whole API: anonymous callers get 401 on every non-public endpoint,
/// and each role gets 403 on the other role's functionality.
/// </summary>
public class RoleAccessTests : ApiTestBase
{
    public RoleAccessTests(RaceDayApiFactory factory) : base(factory) { }

    /// <summary>Organiser-only endpoints.</summary>
    public static TheoryData<string, string> OrganiserEndpoints => new()
    {
        { "POST", "/api/events" },
        { "PUT", "/api/events/1" },
        { "DELETE", "/api/events/1" },
        { "POST", "/api/events/1/categories" },
        { "PUT", "/api/categories/1" },
        { "DELETE", "/api/categories/1" },
        { "GET", "/api/categories/1/enrolments" },
        { "GET", "/api/events/1/enrolments" },
        { "POST", "/api/enrolments/1/result" }
    };

    /// <summary>Participant-only endpoints.</summary>
    public static TheoryData<string, string> ParticipantEndpoints => new()
    {
        { "POST", "/api/categories/1/enrol" },
        { "GET", "/api/users/me/enrolments" },
        { "GET", "/api/users/me/results" }
    };

    /// <summary>Every endpoint that requires a logged-in user.</summary>
    public static TheoryData<string, string> ProtectedEndpoints
    {
        get
        {
            var data = new TheoryData<string, string>
            {
                { "GET", "/api/users/me" },
                { "PUT", "/api/users/me" },
                { "GET", "/api/users/1" },
                { "DELETE", "/api/enrolments/1" }
            };
            foreach (var row in OrganiserEndpoints) data.Add((string)row[0], (string)row[1]);
            foreach (var row in ParticipantEndpoints) data.Add((string)row[0], (string)row[1]);
            return data;
        }
    }

    /// <summary>Endpoints anyone may call.</summary>
    public static TheoryData<string> PublicEndpoints => new()
    {
        "/api/events",
        "/api/events?includePast=true"
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutLoggingIn_Returns401(string method, string url)
    {
        var response = await SendAsync(NewClient(), method, url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(OrganiserEndpoints))]
    public async Task Participant_IsRejectedFromOrganiserFunctionality(string method, string url)
    {
        var (participant, _) = await LoggedInAsync("Participant");

        var response = await SendAsync(participant, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantEndpoints))]
    public async Task Organiser_IsRejectedFromParticipantFunctionality(string method, string url)
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await SendAsync(organiser, method, url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotWithdrawEnrolments()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.DeleteAsync("/api/enrolments/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PublicEndpoints))]
    public async Task PublicEndpoint_WorksWithoutLoggingIn(string url)
    {
        var response = await NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CreatingAnEvent_DoesNotStoreAnything()
    {
        var (participant, _) = await LoggedInAsync("Participant");
        var before = await Factory.WithDbAsync(db => Task.FromResult(db.Events.Count()));

        await participant.PostAsJsonAsync("/api/events", EventBody("Sneaky Event"));

        var after = await Factory.WithDbAsync(db => Task.FromResult(db.Events.Count()));
        Assert.Equal(before, after);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });
        return client.SendAsync(request);
    }
}
