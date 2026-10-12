using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Organisers defining age/distance categories; everyone viewing them.</summary>
public class CategoryTests : ApiTestBase
{
    public CategoryTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Organiser_CanAddADistanceCategory()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", CategoryBody("21km", 21.1m, 250));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await ReadAsync<CategoryDto>(response);
        Assert.Equal(ev.EventId, category.EventId);
        Assert.Equal(21.1m, category.DistanceKm);
        Assert.Equal(250, category.SpotsRemaining);
    }

    [Fact]
    public async Task Organiser_CanAddAnAgeCategory()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            CategoryBody("Under 20", distanceKm: null, maxAge: 19));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await ReadAsync<CategoryDto>(response);
        Assert.Null(category.DistanceKm);
        Assert.Equal(19, category.MaxAge);
    }

    [Fact]
    public async Task AddCategory_WithMinAgeAboveMaxAge_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            CategoryBody("Broken", minAge: 40, maxAge: 20));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ToAMissingEvent_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events/999999/categories", CategoryBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotAddCategoriesToAnotherOrganisersEvent()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{ev.EventId}/categories", CategoryBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Categories_CanBeViewedByEveryone(string? role)
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId);
        var client = role is null ? NewClient() : (await LoggedInAsync(role)).Client;

        var response = await client.GetAsync($"/api/events/{ev.EventId}/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await ReadAsync<List<CategoryDto>>(response));
    }

    [Fact]
    public async Task GetCategories_ForAMissingEvent_Returns404()
    {
        var response = await NewClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateAndDeleteTheirCategory()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var update = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}", CategoryBody("5km", 5m, 80));
        var delete = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("5km", (await ReadAsync<CategoryDto>(update)).CategoryName);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateOrDeleteAnotherOrganisersCategory()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);

        var update = await other.PutAsJsonAsync($"/api/categories/{category.CategoryId}", CategoryBody("Hijacked"));
        var delete = await other.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task UpdateCategory_BelowCurrentEnrolments_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync((await LoggedInAsync("Participant")).Client, category.CategoryId);
        await EnrolAsync((await LoggedInAsync("Participant")).Client, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/categories/{category.CategoryId}", CategoryBody(maxParticipants: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
