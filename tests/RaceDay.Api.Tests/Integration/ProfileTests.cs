using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Viewing and updating your own profile.</summary>
public class ProfileTests : ApiTestBase
{
    public ProfileTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task GetMe_WhenNotLoggedIn_Returns401()
    {
        var response = await NewClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task GetMe_ReturnsTheLoggedInUsersOwnProfile(string role)
    {
        var (client, user) = await LoggedInAsync(role);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await ReadAsync<UserProfileDto>(response);
        Assert.Equal(user.UserId, me.UserId);
        Assert.Equal(user.Email, me.Email);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task UpdateMe_UpdatesTheProfile(string role)
    {
        var (client, user) = await LoggedInAsync(role);

        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Updated Name", phoneNumber = "0820000000" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me", Json);
        Assert.Equal("Updated Name", me!.FullName);
        Assert.Equal("0820000000", me.PhoneNumber);
        Assert.Equal(user.Email, me.Email);
    }

    [Fact]
    public async Task UpdateMe_WithInvalidData_Returns400()
    {
        var (client, _) = await LoggedInAsync("Participant");

        var response = await client.PutAsJsonAsync("/api/users/me", new { fullName = "", phoneNumber = "0820000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_WhenNotLoggedIn_Returns401()
    {
        var response = await NewClient().PutAsJsonAsync("/api/users/me", new { fullName = "Someone" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task GetUserById_AsNonAdmin_Returns403(string role)
    {
        var (client, user) = await LoggedInAsync(role);

        var response = await client.GetAsync($"/api/users/{user.UserId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_AsAdmin_Returns200Or404()
    {
        var (_, participant) = await LoggedInAsync("Participant");
        var admin = await LoggedInAsAdminAsync();

        var found = await admin.GetAsync($"/api/users/{participant.UserId}");
        var missing = await admin.GetAsync("/api/users/999999");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
