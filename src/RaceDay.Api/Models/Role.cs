namespace RaceDay.Api.Models;

/// <summary>A system role (Organiser, Participant or Admin). Maps to dbo.Roles.</summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
