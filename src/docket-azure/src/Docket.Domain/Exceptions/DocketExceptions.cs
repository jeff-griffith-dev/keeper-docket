namespace Docket.Domain.Exceptions;

/// <summary>
/// Base class for all Docket domain exceptions.
/// Each exception maps to a specific API error code returned to callers.
/// </summary>
public abstract class DocketException(string errorCode, string message)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

public class MinutesFinalizedException(Guid minutesId)
    : DocketException("MINUTES_FINALIZED",
        $"Minutes {minutesId} has been finalized and cannot be modified.");

public class MinutesAbandonedException(Guid minutesId)
    : DocketException("MINUTES_ABANDONED",
        $"Minutes {minutesId} has been abandoned and cannot be modified.");

public class SeriesArchivedException(Guid seriesId)
    : DocketException("SERIES_ARCHIVED",
        $"MeetingSeries {seriesId} is archived. No new Minutes can be created and no records can be edited.");

public class InvalidStatusTransitionException(string from, string to)
    : DocketException("INVALID_STATUS_TRANSITION",
        $"Cannot transition from '{from}' to '{to}'.");

public class SystemLabelException()
    : DocketException("SYSTEM_LABEL",
        "System labels cannot be deleted or modified.");

public class SingleModeratorException()
    : DocketException("SINGLE_MODERATOR",
        "This operation would leave the series with no moderator. Transfer the moderator role before removing.");

public class InformedOwnerException()
    : DocketException("INFORMED_OWNER",
        "An Informed participant cannot own an action item.");

public class InformedAttendeeException()
    : DocketException("INFORMED_ATTENDEE",
        "An Informed participant cannot be recorded as a meeting attendee.");

public class UnresolvedDraftsException(IReadOnlyList<UnresolvedDraftInfo> drafts)
    : DocketException("UNRESOLVED_DRAFTS_EXIST",
        "One or more draft Minutes must be finalized or abandoned before this operation.")
{
    public IReadOnlyList<UnresolvedDraftInfo> UnresolvedDrafts { get; } = drafts;
}

public record UnresolvedDraftInfo(Guid Id, DateTimeOffset ScheduledFor);

public class AbandonmentNoteRequiredException()
    : DocketException("ABANDONMENT_NOTE_REQUIRED",
        "A non-empty note is required when abandoning Minutes. The reason must be on record.");

public class DuplicateParticipantException(Guid userId, Guid seriesId)
    : DocketException("DUPLICATE_PARTICIPANT",
        $"User {userId} is already a participant in series {seriesId}.");

public class DuplicateAttendeeException(Guid userId, Guid minutesId)
    : DocketException("DUPLICATE_ATTENDEE",
        $"User {userId} is already recorded as an attendee for Minutes {minutesId}.");

public class DuplicateLabelException(string name)
    : DocketException("DUPLICATE_LABEL",
        $"A label named '{name}' already exists.");

public class LabelAlreadyAppliedException(Guid labelId, Guid actionItemId)
    : DocketException("LABEL_ALREADY_APPLIED",
        $"Label {labelId} is already applied to action item {actionItemId}.");

public class EmailExistsException(string email)
    : DocketException("EMAIL_EXISTS",
        $"A user with email '{email}' already exists.");

public class NotFoundException(string resourceType, Guid id)
    : DocketException("NOT_FOUND",
        $"{resourceType} {id} was not found.");

public class ForbiddenException(string reason)
    : DocketException("FORBIDDEN", reason);