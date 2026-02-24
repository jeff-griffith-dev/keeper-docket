namespace Docket.Domain.Entities;

/// <summary>
/// Identity record for anyone who interacts with Docket.
/// No global role — permissions are contextual via SeriesParticipant.
/// ExternalId reserved for future federation (LDAP, Azure AD, OAuth).
/// </summary>
public class User : EntityBase
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ExternalId { get; set; }

    // Navigation
    public ICollection<SeriesParticipant> Participations { get; set; } = [];
    public ICollection<ActionItem> OwnedActionItems { get; set; } = [];
}
