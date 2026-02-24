namespace Docket.Domain.Entities;

/// <summary>
/// Records who actually attended a specific meeting.
/// Absence is derived: SeriesParticipants (Moderator + Invited) minus MinutesAttendees.
/// Informed participants are never recorded as attendees — they are never expected to attend.
/// </summary>
public class MinutesAttendee
{
    public Guid MinutesId { get; set; }
    public Guid UserId { get; set; }

    // Navigation
    public Minutes? Minutes { get; set; }
    public User? User { get; set; }
}
