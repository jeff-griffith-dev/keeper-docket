using Docket.Domain.Enums;

namespace Docket.Domain.Entities;

/// <summary>
/// Timestamped log entry on an ActionItem. Append-only — never edited or deleted.
/// The timestamp is part of the meaning: it records when this was said about
/// this commitment.
///
/// Phase is set by the service layer based on the parent Minutes status at the
/// time the note is created. Callers never supply it.
///
///   Meeting     — created while parent Minutes was Draft. Closed set after finalization.
///                 The finalization email renders only Meeting-phase notes.
///   PostMeeting — created after parent Minutes was Finalized or Abandoned.
///                 Context that arrived after the record closed.
///
/// Notes may be appended to items in both Draft and Finalized Minutes.
/// This is the one deliberate exception to general immutability — it allows
/// Keeper and participants to add follow-up context without re-finalization.
/// </summary>
public class ActionItemNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ActionItemId { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Server-set. Never supplied by callers.
    /// </summary>
    public NotePhase Phase { get; set; }

    public Guid AuthorId { get; set; }

    /// <summary>
    /// Immutable. Set by the server at creation. The timestamp is the record.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation
    public ActionItem? ActionItem { get; set; }
    public User? Author { get; set; }
}
