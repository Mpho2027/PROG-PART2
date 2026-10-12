using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>A user's profile. Never includes the password hash.</summary>
public class UserProfileDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static UserProfileDto From(User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        PhoneNumber = u.PhoneNumber,
        Role = u.Role.RoleName,
        CreatedAt = u.CreatedAt
    };
}

/// <summary>Profile fields a user may change on their own account.</summary>
public class UpdateProfileRequest
{
    /// <example>Sarah Botha-Smith</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>0739998888</example>
    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }
}
