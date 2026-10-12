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
        var hash = _hasher.HashPassword(_user, "Password123!");

        Assert.NotEqual("Password123!", hash);
        Assert.DoesNotContain("Password123!", hash);
    }

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently()
    {
        Assert.NotEqual(_hasher.HashPassword(_user, "Password123!"), _hasher.HashPassword(_user, "Password123!"));
    }

    [Fact]
    public void Verify_AcceptsTheCorrectPasswordAndRejectsAWrongOne()
    {
        var hash = _hasher.HashPassword(_user, "Password123!");

        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_user, hash, "Password123!"));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(_user, hash, "password123!"));
    }
}
