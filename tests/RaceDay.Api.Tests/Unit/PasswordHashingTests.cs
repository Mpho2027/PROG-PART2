using Microsoft.AspNetCore.Identity;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests.Unit;

/// <summary>The password hasher the API uses never stores or reproduces the plain-text password.</summary>
public class PasswordHashingTests
{
    private readonly PasswordHasher<User> _hasher = new();
    private readonly User _user = new() { Email = "test@test.co.za" };

    [Fact]
    public void Hash_IsNotThePlainTextPassword()
    {
        var hash = _hasher.HashPassword(_user, "Racing2026!");

        Assert.NotEqual("Racing2026!", hash);
        Assert.DoesNotContain("Racing2026!", hash);
    }

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently()
    {
        Assert.NotEqual(_hasher.HashPassword(_user, "Racing2026!"), _hasher.HashPassword(_user, "Racing2026!"));
    }

    [Fact]
    public void Verify_AcceptsTheCorrectPasswordAndRejectsAWrongOne()
    {
        var hash = _hasher.HashPassword(_user, "Racing2026!");

        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, hash, "Racing2026!"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(_user, hash, "password123!"));
    }
}
