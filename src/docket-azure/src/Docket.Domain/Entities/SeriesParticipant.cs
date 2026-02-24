using Docket.Domain.Enums;

namespace Docket.Domain.Entities;

/// <summary>
/// Join entity between User and MeetingSeries carrying the participant's role.
/// This is not a simple many-to-many — the role drives authorization, email
/// distribution, and action item ownership constraints throughout the system.
///
/// Role semantics:
///   Moderator — can create/finalize/abandon Minutes, owns the series, receives all emails.
///               Exactly one per series at all times.
///   Invited   — attends meetings, can add topics/items, can own action items, receives all emails.
///   Informed  — receives finalized minutes only. Cannot own action items. Never recorded as attendee.
/// </summary>
public class SeriesParticipant
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SeriesId { get; set; }
    public Guid UserId { get; set; }
    public ParticipantRole Role { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    // Navigation
    public MeetingSeries? Series { get; set; }
    public User? User { get; set; }
}
