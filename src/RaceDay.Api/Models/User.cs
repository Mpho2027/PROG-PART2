namespace RaceDay.Api.Models;

/// <summary>A registered user. Maps to dbo.Users.</summary>
public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by ASP.NET Core's PasswordHasher. The plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Role Role { get; set; } = null!;

    /// <summary>Events this user organises (Organiser role only).</summary>
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();

    /// <summary>Enrolments this user holds (Participant role only).</summary>
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}
