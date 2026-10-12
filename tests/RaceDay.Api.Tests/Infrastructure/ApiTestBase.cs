using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Base class for API tests. Each HttpClient keeps its own cookie jar, so a client that logs in
/// carries that user's session cookie on every later request, exactly like a browser.
/// </summary>
public abstract class ApiTestBase : IClassFixture<RaceDayApiFactory>
{
    protected const string Password = "Password123!";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected RaceDayApiFactory Factory { get; }

    protected ApiTestBase(RaceDayApiFactory factory) => Factory = factory;

    /// <summary>A fresh client with an empty cookie jar (an anonymous visitor).</summary>
    protected HttpClient NewClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    protected static string UniqueEmail(string prefix = "user") => $"{prefix}.{Guid.NewGuid():N}@test.co.za";

    protected static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string role, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/register", new { fullName = $"Test {role}", email, password, role });

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    /// <summary>Registers a new user with the given role and returns a client logged in as them.</summary>
    protected async Task<(HttpClient Client, UserProfileDto User)> LoggedInAsync(string role)
    {
        var client = NewClient();
        var email = UniqueEmail(role.ToLowerInvariant());
        var register = await RegisterAsync(client, role, email);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync<LoginResponse>(login);
        return (client, body.User);
    }

    protected async Task<HttpClient> LoggedInAsAdminAsync()
    {
        var client = NewClient();
        var login = await LoginAsync(client, RaceDayApiFactory.AdminEmail, RaceDayApiFactory.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    protected static object EventBody(string name = "Test Race", int daysFromToday = 30, string eventType = "Run", decimal distanceKm = 21.1m) => new
    {
        eventName = name,
        description = "A test event.",
        eventDate = DateTime.Today.AddDays(daysFromToday).ToString("yyyy-MM-dd"),
        location = "Johannesburg, Gauteng",
        distanceKm,
        eventType
    };

    protected static object CategoryBody(string name = "10km", decimal? distanceKm = 10m, int maxParticipants = 100,
        int? minAge = null, int? maxAge = null) => new
    {
        categoryName = name,
        distanceKm,
        minAge,
        maxAge,
        maxParticipants,
        entryFee = 120m
    };

    protected static async Task<EventDto> CreateEventAsync(HttpClient organiser, int daysFromToday = 30)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(daysFromToday: daysFromToday));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<EventDto>(response);
    }

    protected static async Task<CategoryDto> CreateCategoryAsync(HttpClient organiser, int eventId, int maxParticipants = 100)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", CategoryBody(maxParticipants: maxParticipants));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CategoryDto>(response);
    }

    protected static async Task<EnrolmentDto> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await participant.PostAsync($"/api/categories/{categoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<EnrolmentDto>(response);
    }

    /// <summary>
    /// Puts Participants into one category of an event (owned by organiserId) that took place three days ago.
    /// The API correctly refuses enrolment into past events, so this arranges the data directly.
    /// </summary>
    protected Task<(int CategoryId, int[] EnrolmentIds)> SeedPastEnrolmentsAsync(int organiserId, params int[] participantIds) =>
        Factory.WithDbAsync(async db =>
        {
            var ev = new Event
            {
                OrganiserId = organiserId, EventName = "Completed Race", EventDate = DateTime.Today.AddDays(-3),
                Location = "Durban, KwaZulu-Natal", DistanceKm = 10m, EventType = EventType.Run
            };
            var category = new Category { Event = ev, CategoryName = "10km", DistanceKm = 10m, MaxParticipants = 50 };
            var enrolments = participantIds
                .Select(id => new EventEnrolment { ParticipantId = id, Category = category, BibNumber = $"T{id}" })
                .ToList();
            db.EventEnrolments.AddRange(enrolments);
            await db.SaveChangesAsync();
            return (category.CategoryId, enrolments.Select(e => e.EnrolmentId).ToArray());
        });

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}
