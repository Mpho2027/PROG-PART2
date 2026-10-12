using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Organisers managing events; everyone viewing them.</summary>
public class EventManagementTests : ApiTestBase
{
    public EventManagementTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Organiser_CanCreateAnEvent_WithAllRequiredFields()
    {
        var (organiser, user) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", EventBody("Soweto Cycle Tour", 45, "Cycle", 94.7m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var ev = await ReadAsync<EventDto>(response);
        Assert.Equal("Soweto Cycle Tour", ev.EventName);
        Assert.Equal(EventType.Cycle, ev.EventType);
        Assert.Equal(94.7m, ev.DistanceKm);
        Assert.Equal(DateTime.Today.AddDays(45), ev.EventDate);
        Assert.Equal("Johannesburg, Gauteng", ev.Location);
        Assert.Equal("A test event.", ev.Description);
        Assert.Equal(user.UserId, ev.OrganiserId);

        var stored = await Factory.WithDbAsync(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.Equal(user.UserId, stored.OrganiserId);
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Walk")]
    [InlineData("Cycle")]
    public async Task Organiser_CanCreateEachEventType(string type)
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(eventType: type));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithAnUnknownEventType_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(eventType: "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithMissingFields_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new { eventName = "No date or location" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateTheirOwnEvent()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{ev.EventId}", EventBody("Renamed Race", 50, "Walk", 5m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EventDto>(response);
        Assert.Equal("Renamed Race", updated.EventName);
        Assert.Equal(EventType.Walk, updated.EventType);
        Assert.Equal(5m, updated.DistanceKm);
    }

    [Fact]
    public async Task Organiser_CanDeleteTheirOwnEvent_AndItsCategories()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.False(await Factory.WithDbAsync(db => db.Categories.AnyAsync(c => c.CategoryId == category.CategoryId)));
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", EventBody("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var stored = await Factory.WithDbAsync(db => db.Events.SingleAsync(e => e.EventId == ev.EventId));
        Assert.NotEqual("Hijacked", stored.EventName);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);

        var response = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await Factory.WithDbAsync(db => db.Events.AnyAsync(e => e.EventId == ev.EventId)));
    }

    [Fact]
    public async Task UpdateOrDelete_ANonExistentEvent_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var update = await organiser.PutAsJsonAsync("/api/events/999999", EventBody());
        var delete = await organiser.DeleteAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Events_CanBeViewedWithoutLoggingIn()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var anonymous = NewClient();

        var list = await anonymous.GetAsync("/api/events");
        var single = await anonymous.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(await ReadAsync<List<EventDto>>(list), e => e.EventId == ev.EventId);
        Assert.Equal(HttpStatusCode.OK, single.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task BothRoles_CanViewEvents(string role)
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var (client, _) = await LoggedInAsync(role);

        var response = await client.GetAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ExcludesPastEventsUnlessRequested()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var past = await CreateEventAsync(organiser, daysFromToday: -10);

        var upcoming = await NewClient().GetFromJsonAsync<List<EventDto>>("/api/events", Json);
        var all = await NewClient().GetFromJsonAsync<List<EventDto>>("/api/events?includePast=true", Json);

        Assert.DoesNotContain(upcoming!, e => e.EventId == past.EventId);
        Assert.Contains(all!, e => e.EventId == past.EventId);
    }

    [Fact]
    public async Task GetEvent_ThatDoesNotExist_Returns404()
    {
        var response = await NewClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
