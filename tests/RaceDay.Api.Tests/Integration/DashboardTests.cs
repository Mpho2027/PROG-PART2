using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>The Organiser dashboard summary (GET /api/users/me/events).</summary>
public class DashboardTests : ApiTestBase
{
    public DashboardTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Dashboard_ListsOnlyTheOrganisersOwnEvents_WithEnrolmentTotals()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var (otherOrganiser, _) = await LoggedInAsync("Organiser");
        var (participant, _) = await LoggedInAsync("Participant");

        var ev = await CreateEventAsync(organiser, daysFromToday: 20);
        var tenK = await CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 50);
        await CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 30);
        await EnrolAsync(participant, tenK.CategoryId);
        var empty = await CreateEventAsync(organiser, daysFromToday: 40);
        var notMine = await CreateEventAsync(otherOrganiser, daysFromToday: 10);

        var summary = (await organiser.GetFromJsonAsync<List<OrganiserEventSummaryDto>>("/api/users/me/events", Json))!;

        Assert.Equal(new[] { ev.EventId, empty.EventId }, summary.Select(e => e.EventId));
        Assert.DoesNotContain(summary, e => e.EventId == notMine.EventId);

        var row = summary.Single(e => e.EventId == ev.EventId);
        Assert.Equal(2, row.CategoryCount);
        Assert.Equal(1, row.EnrolmentCount);
        Assert.Equal(80, row.Capacity);
        Assert.True(row.IsUpcoming);

        var emptyRow = summary.Single(e => e.EventId == empty.EventId);
        Assert.Equal(0, emptyRow.CategoryCount);
        Assert.Equal(0, emptyRow.EnrolmentCount);
        Assert.Equal(0, emptyRow.Capacity);
    }

    [Fact]
    public async Task Dashboard_ListsUpcomingEventsBeforePastEvents()
    {
        var (organiser, user) = await LoggedInAsync("Organiser");
        var (_, participant) = await LoggedInAsync("Participant");
        var later = await CreateEventAsync(organiser, daysFromToday: 60);
        var sooner = await CreateEventAsync(organiser, daysFromToday: 5);
        await SeedPastEnrolmentsAsync(user.UserId, participant.UserId);

        var summary = await organiser.GetFromJsonAsync<List<OrganiserEventSummaryDto>>("/api/users/me/events", Json);

        Assert.Equal(3, summary!.Count);
        Assert.Equal(sooner.EventId, summary[0].EventId);
        Assert.Equal(later.EventId, summary[1].EventId);
        Assert.False(summary[2].IsUpcoming);
    }

    [Fact]
    public async Task Dashboard_CountsCapturedResults()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, p1) = await LoggedInAsync("Participant");
        var (_, p2) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, p1.UserId, p2.UserId);

        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:45:10", position = 1 });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var summary = await organiser.GetFromJsonAsync<List<OrganiserEventSummaryDto>>("/api/users/me/events", Json);
        var row = Assert.Single(summary!);
        Assert.Equal(2, row.EnrolmentCount);
        Assert.Equal(1, row.ResultsCaptured);
        Assert.False(row.IsUpcoming);
    }
}
