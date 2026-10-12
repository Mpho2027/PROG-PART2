using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>User registration, login, logout and session handling.</summary>
public class AuthenticationTests : ApiTestBase
{
    public AuthenticationTests(RaceDayApiFactory factory) : base(factory) { }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_WithValidDetails_Returns201AndTheChosenRole(string role)
    {
        var email = UniqueEmail();

        var response = await RegisterAsync(NewClient(), role, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await ReadAsync<UserProfileDto>(response);
        Assert.Equal(role, user.Role);
        Assert.Equal(email, user.Email);
        Assert.True(user.UserId > 0);
    }

    [Fact]
    public async Task Register_ResponseNeverContainsThePassword()
    {
        var response = await RegisterAsync(NewClient(), "Participant", UniqueEmail());

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, raw);
    }

    [Fact]
    public async Task Register_StoresAHashedPasswordNotThePlainText()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), "Participant", email);

        var user = await Factory.WithDbAsync(db => db.Users.SingleAsync(u => u.Email == email));

        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
        var verify = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, Password);
        Assert.NotEqual(PasswordVerificationResult.Failed, verify);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        var email = UniqueEmail();
        await RegisterAsync(NewClient(), "Participant", email);

        var response = await RegisterAsync(NewClient(), "Organiser", email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperUser")]
    [InlineData("")]
    public async Task Register_WithARoleThatCannotBeSelfAssigned_Returns400(string role)
    {
        var response = await RegisterAsync(NewClient(), role, UniqueEmail());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidEmailOrShortPassword_Returns400()
    {
        var badEmail = await RegisterAsync(NewClient(), "Participant", "not-an-email");
        var shortPassword = await RegisterAsync(NewClient(), "Participant", UniqueEmail(), "short");

        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndSetsAnHttpOnlySessionCookie()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, "Organiser", email);

        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync<LoginResponse>(response);
        Assert.Equal("Organiser", body.User.Role);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".RaceDay.Session="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_SessionKeepsTheUserAndRoleForLaterRequests()
    {
        var (client, user) = await LoggedInAsync("Participant");

        var me = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me", Json);

        Assert.Equal(user.UserId, me!.UserId);
        Assert.Equal("Participant", me.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, "Participant", email);

        var response = await LoginAsync(client, email, "WrongPassword!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await LoginAsync(NewClient(), UniqueEmail("nobody"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_FailedAttempt_DoesNotAuthenticateTheClient()
    {
        var client = NewClient();
        var email = UniqueEmail();
        await RegisterAsync(client, "Participant", email);
        await LoginAsync(client, email, "WrongPassword!");

        var me = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _) = await LoggedInAsync("Participant");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);
        var after = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task ForgedSessionCookie_IsNotAuthenticated()
    {
        var client = NewClient();
        client.DefaultRequestHeaders.Add("Cookie", ".RaceDay.Session=forged-value");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
