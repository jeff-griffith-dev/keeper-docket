using Docket.Domain.Enums;

namespace Docket.Domain.Entities;

/// <summary>
/// Top-level container. Maps to a single calendar series in Outlook/Google Calendar.
/// A Professional Services engagement typically involves multiple parallel series.
/// Engagement-level grouping is deferred to v2 — see ADR-005.
/// </summary>
public class MeetingSeries : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Project { get; set; }
    public SeriesStatus Status { get; set; } = SeriesStatus.Active;

    /// <summary>
    /// Outlook or Google Calendar series ID. Unused in v1 beyond storage.
    /// In v2, Keeper will use this to propose engagement groupings. See ADR-005.
    /// </summary>
    public string? ExternalCalendarId { get; set; }

    public Guid CreatedBy { get; set; }

    // Navigation
    public User? CreatedByUser { get; set; }
    public ICollection<SeriesParticipant> Participants { get; set; } = [];
    public ICollection<Minutes> Minutes { get; set; } = [];
}
