using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Details supplied when registering a new account.</summary>
public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored only as a salted hash.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant". The Admin role cannot be self-assigned.</summary>
    /// <example>Participant</example>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Optional contact number.</summary>
    /// <example>0731112222</example>
    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }
}

/// <summary>Credentials used to log in.</summary>
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned after a successful login. The session cookie is set on the response.</summary>
public class LoginResponse
{
    public string Message { get; set; } = string.Empty;
    public UserProfileDto User { get; set; } = null!;
}

/// <summary>An image file sent as multipart/form-data (field name "file").</summary>
public class ImageUploadRequest
{
    /// <summary>JPG, PNG, GIF or WEBP image, at most 5 MB.</summary>
    [Required]
    public IFormFile? File { get; set; }
}

/// <summary>A simple message body, used for errors and confirmations.</summary>
public record MessageResponse(string Message);
