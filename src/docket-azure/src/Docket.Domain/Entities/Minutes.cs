using Docket.Domain.Enums;
using Docket.Domain.Exceptions;

namespace Docket.Domain.Entities;

/// <summary>
/// The complete record of a single meeting instance within a MeetingSeries.
/// Exists in two phases: before the meeting (a plan) and after (a record).
/// Finalization is the irreversible transition from plan to record.
///
/// Minutes are never deleted. A draft that will not be finalized must be
/// Abandoned with a required note explaining why.
///
/// State machine:
///   Draft → Finalized  (irreversible, immutable after)
///   Draft → Abandoned  (irreversible, requires non-empty note)
///
/// Pre-creation gate: creating new Minutes is blocked if any prior Minutes
/// in the series chain is still in Draft status. See MinutesService.
/// </summary>
public class Minutes : EntityBase
{
    public Guid SeriesId { get; set; }

    /// <summary>
    /// Explicit linked-list pointer to the prior Minutes in this series.
    /// Chain is traversed, not date-filtered. There is no guarantee dates
    /// are contiguous — meetings are cancelled, rescheduled, or abandoned.
    /// </summary>
    public Guid? PreviousMinutesId { get; set; }

    public DateTimeOffset ScheduledFor { get; set; }
    public MinutesStatus Status { get; private set; } = MinutesStatus.Draft;

    /// <summary>
    /// 0 while Draft. Set to 1 on first finalization.
    /// Reserved for potential re-finalization in future versions.
    /// </summary>
    public int Version { get; private set; } = 0;

    public DateTimeOffset? FinalizedAt { get; private set; }
    public Guid? FinalizedBy { get; private set; }

    public DateTimeOffset? AbandonedAt { get; private set; }
    public Guid? AbandonedBy { get; private set; }

    /// <summary>
    /// Required on abandonment. The permanent record of why this meeting
    /// was never finalized.
    /// </summary>
    public string? AbandonmentNote { get; private set; }

    public string? GlobalNote { get; set; }
    public bool GlobalNotePinned { get; set; } = false;

    // Navigation
    public MeetingSeries? Series { get; set; }
    public Minutes? PreviousMinutes { get; set; }
    public ICollection<MinutesAttendee> Attendees { get; set; } = [];
    public ICollection<Topic> Topics { get; set; } = [];

    // -------------------------------------------------------------------------
    // State machine methods — all business rule enforcement lives here
    // -------------------------------------------------------------------------

    public void Finalize(Guid finalizedBy)
    {
        if (Status == MinutesStatus.Finalized)
            throw new InvalidStatusTransitionException(Status.ToString(), "Finalized");
        if (Status == MinutesStatus.Abandoned)
            throw new MinutesAbandonedException(Id);

        Status = MinutesStatus.Finalized;
        Version = 1;
        FinalizedAt = DateTimeOffset.UtcNow;
        FinalizedBy = finalizedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Abandon(Guid abandonedBy, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new AbandonmentNoteRequiredException();
        if (Status == MinutesStatus.Finalized)
            throw new MinutesFinalizedException(Id);
        if (Status == MinutesStatus.Abandoned)
            throw new MinutesAbandonedException(Id);

        Status = MinutesStatus.Abandoned;
        AbandonedAt = DateTimeOffset.UtcNow;
        AbandonedBy = abandonedBy;
        AbandonmentNote = note;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsEditable => Status == MinutesStatus.Draft;

    public void EnsureEditable()
    {
        if (Status == MinutesStatus.Finalized)
            throw new MinutesFinalizedException(Id);
        if (Status == MinutesStatus.Abandoned)
            throw new MinutesAbandonedException(Id);
    }
}
