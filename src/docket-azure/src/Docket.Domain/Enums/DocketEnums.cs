namespace Docket.Domain.Enums;

public enum SeriesStatus
{
    Active,
    Archived
}

public enum ParticipantRole
{
    Moderator,
    Invited,
    Informed
}

public enum MinutesStatus
{
    Draft,
    Finalized,
    Abandoned
}

public enum ActionItemStatus
{
    Open,
    Done,
    Deferred,
    Abandoned  // System-set only. Never settable by callers directly.
}

public enum NotePhase
{
    Meeting,      // Created while parent Minutes was Draft. Closed set after finalization.
    PostMeeting   // Created after parent Minutes was Finalized or Abandoned.
}

public enum LabelCategory
{
    Action,   // Decision, Proposal, New — classifies the type of commitment
    Status    // Status:GREEN, Status:YELLOW, Status:RED — traffic-light indicator
}