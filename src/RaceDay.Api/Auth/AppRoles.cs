namespace RaceDay.Api.Auth;

/// <summary>Role names and their fixed IDs in dbo.Roles.</summary>
public static class AppRoles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
    public const string Admin = "Admin";

    public const int OrganiserId = 1;
    public const int ParticipantId = 2;
    public const int AdminId = 3;

    /// <summary>Roles a member of the public may choose when registering. Admin is never self-assignable.</summary>
    public static readonly IReadOnlyDictionary<string, int> Registerable =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Organiser] = OrganiserId,
            [Participant] = ParticipantId
        };
}
