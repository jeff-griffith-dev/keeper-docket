using Docket.Domain.Enums;
using Docket.Domain.Exceptions;

namespace Docket.Domain.Entities;

/// <summary>
/// A commitment: something a named person agreed to do, by a specific date.
/// The core unit of value in Docket.
///
/// Every action item has exactly one owner (ResponsibleId). Shared ownership
/// is not supported — if two people need to do something, that is two items.
///
/// State machine:
///   Open     → Done      (terminal, caller-initiated)
///   Open    ↔  Deferred  (caller-initiated)
///   Open     → Abandoned (system-set only — fires on Minutes abandonment or Series archival)
///   Deferred → Abandoned (system-set only)
///
/// Abandoned items are terminal and immutable. The clone-and-act pattern is
/// the resolution path: create a new ActionItem in a future Minutes with
/// SourceActionItemId pointing back to preserve the lineage.
/// </summary>
public class ActionItem : EntityBase
{
    public Guid TopicId { get; set; }

    /// <summary>
    /// If this item was carried forward from a prior meeting, points to the
    /// item it was created from. Use GET /action-items/{id}/history to
    /// traverse the full chain.
    /// </summary>
    public Guid? SourceActionItemId { get; set; }

    public string Title { get; set; } = string.Empty;
    public Guid ResponsibleId { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// 1=Critical, 2=High, 3=Medium (default), 4=Low, 5=Minimal
    /// </summary>
    public int Priority { get; set; } = 3;

    public ActionItemStatus Status { get; private set; } = ActionItemStatus.Open;
    public bool IsRecurring { get; set; } = false;

    /// <summary>
    /// True if the responsible user was not present at the meeting.
    /// Does not change behavior — assignment in absentia is permitted.
    /// Surfaced in notification emails for context.
    /// </summary>
    public bool AssignedInAbsentia { get; set; } = false;

    public Guid CreatedBy { get; set; }

    // Navigation
    public Topic? Topic { get; set; }
    public ActionItem? SourceActionItem { get; set; }
    public User? Responsible { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<ActionItemNote> Notes { get; set; } = [];
    public ICollection<ActionItemLabel> ActionItemLabels { get; set; } = [];

    // -------------------------------------------------------------------------
    // State machine methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Caller-initiated: mark the commitment as completed.
    /// </summary>
    public void MarkDone()
    {
        if (Status != ActionItemStatus.Open)
            throw new InvalidStatusTransitionException(Status.ToString(), "Done");

        Status = ActionItemStatus.Done;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Caller-initiated: defer the commitment to a future meeting.
    /// </summary>
    public void Defer()
    {
        if (Status != ActionItemStatus.Open)
            throw new InvalidStatusTransitionException(Status.ToString(), "Deferred");

        Status = ActionItemStatus.Deferred;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Caller-initiated: reopen a deferred commitment.
    /// </summary>
    public void Reopen()
    {
        if (Status != ActionItemStatus.Deferred)
            throw new InvalidStatusTransitionException(Status.ToString(), "Open");

        Status = ActionItemStatus.Open;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// System-initiated only. Fires when the parent Minutes is abandoned
    /// or the parent MeetingSeries is archived.
    /// Not callable by API clients directly.
    /// </summary>
    /// <summary>
    /// Convention: call only from system-level operations (series archival,
    /// Minutes abandonment). Not intended for direct caller use — enforce
    /// this via code review and API-layer guards, not language visibility,
    /// since the call site is in a different assembly.
    /// </summary>
    public void AbandonBySystem()
    {
        if (Status is ActionItemStatus.Done or ActionItemStatus.Abandoned)
            return; // Idempotent — already terminal

        Status = ActionItemStatus.Abandoned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// System-initiated only. Called when this item is carried forward to
    /// a new Minutes. Sets the original to Deferred so the chain reads:
    /// this item was open, it was carried forward, it is now deferred here.
    /// The carried-forward copy is the active item.
    /// Not callable by API clients directly.
    /// </summary>
    /// <summary>
    /// Convention: call only from the carry-forward logic in MinutesEndpoints.
    /// Marks the original item as Deferred when it is carried to a new Minutes.
    /// </summary>
    public void MarkDeferredByCarryForward()
    {
        if (Status != ActionItemStatus.Open)
            return; // Only open items are carried forward; guard against double-calls

        Status = ActionItemStatus.Deferred;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool IsTerminal =>
        Status is ActionItemStatus.Done or ActionItemStatus.Abandoned;
}
