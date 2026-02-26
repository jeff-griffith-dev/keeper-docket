using Docket.Api.Services;
using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Docket.Domain.Exceptions;
using Docket.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Docket.Api.Endpoints;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public record MinutesSummaryResponse(
    Guid Id,
    Guid SeriesId,
    Guid? PreviousMinutesId,
    DateTimeOffset ScheduledFor,
    string Status,
    int Version,
    DateTimeOffset? FinalizedAt,
    int OpenTopicCount,
    int OpenActionItemCount,
    DateTimeOffset CreatedAt)
{
    public static MinutesSummaryResponse From(Minutes m) => new(
        m.Id,
        m.SeriesId,
        m.PreviousMinutesId,
        m.ScheduledFor,
        m.Status.ToString(),
        m.Version,
        m.FinalizedAt,
        m.Topics.Count(t => t.IsOpen),
        m.Topics.SelectMany(t => t.ActionItems).Count(a => a.Status == ActionItemStatus.Open),
        m.CreatedAt);
}

public record MinutesDetailResponse(
    Guid Id,
    Guid SeriesId,
    Guid? PreviousMinutesId,
    DateTimeOffset ScheduledFor,
    string Status,
    int Version,
    DateTimeOffset? FinalizedAt,
    Guid? FinalizedBy,
    DateTimeOffset? AbandonedAt,
    Guid? AbandonedBy,
    string? AbandonmentNote,
    string? GlobalNote,
    bool GlobalNotePinned,
    IEnumerable<TopicResponse> Topics,
    IEnumerable<AttendeeResponse> Attendees,
    IEnumerable<UserResponse> AbsentParticipants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record AttendeeResponse(Guid UserId, UserResponse User);

public record CreateMinutesRequest(
    Guid SeriesId,
    DateTimeOffset ScheduledFor,
    string? GlobalNote);

public record UpdateMinutesRequest(
    DateTimeOffset? ScheduledFor,
    string? GlobalNote,
    bool? GlobalNotePinned);

public record FinalizeMinutesRequest(bool NotifyResponsibles, bool NotifyAll);
public record AbandonMinutesRequest(string Note);

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

public static class MinutesEndpoints
{
    public static void MapMinutesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/minutes").WithTags("minutes");

        group.MapPost("/", CreateMinutes)
            .WithName("CreateMinutes")
            .WithSummary("Create a new Minutes for a series");

        group.MapGet("/{minutesId:guid}", GetMinutes)
            .WithName("GetMinutes")
            .WithSummary("Get a Minutes record with all topics and items");

        group.MapPatch("/{minutesId:guid}", UpdateMinutes)
            .WithName("UpdateMinutes")
            .WithSummary("Update a draft Minutes record");

        group.MapPost("/{minutesId:guid}/finalize", FinalizeMinutes)
            .WithName("FinalizeMinutes")
            .WithSummary("Finalize a Minutes record — irreversible");

        group.MapPost("/{minutesId:guid}/abandon", AbandonMinutes)
            .WithName("AbandonMinutes")
            .WithSummary("Abandon a draft Minutes record — irreversible, requires note");

        group.MapGet("/{minutesId:guid}/attendees", ListAttendees)
            .WithName("ListAttendees")
            .WithSummary("List attendees for a Minutes");

        group.MapPost("/{minutesId:guid}/attendees", AddAttendee)
            .WithName("AddAttendee")
            .WithSummary("Record that a participant attended this meeting");

        group.MapDelete("/{minutesId:guid}/attendees/{userId:guid}", RemoveAttendee)
            .WithName("RemoveAttendee")
            .WithSummary("Remove an attendance record");
    }

    // POST /minutes
    private static async Task<IResult> CreateMinutes(
        CreateMinutesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if(request.SeriesId == Guid.Empty)
            throw new ArgumentMissingException(nameof(request.SeriesId));

        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == request.SeriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), request.SeriesId);

        SeriesEndpoints.EnsureModerator(series, currentUser.UserId);

        if (series.Status == SeriesStatus.Archived)
            throw new SeriesArchivedException(request.SeriesId);

        // Pre-creation gate: no unresolved drafts anywhere in the chain
        await SeriesEndpoints.EnsureNoUnresolvedDraftsAsync(request.SeriesId, db, ct);

        // Find the most recent Minutes to set previousMinutesId and seed carry-forward
        var candidates = await db.Minutes
            .Where(m => m.SeriesId == request.SeriesId)
            .Include(m => m.Topics)
                .ThenInclude(t => t.ActionItems)
            .ToListAsync(ct);
        var latestMinutes = candidates
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();

        candidates = await db.Minutes
            .Where(m => m.SeriesId == request.SeriesId &&
                        m.Status == MinutesStatus.Finalized)
            .Include(m => m.Topics)
                .ThenInclude(t => t.ActionItems)
                    .ThenInclude(a => a.ActionItemLabels)
            .ToListAsync(ct);
        var lastFinalized = candidates
            .OrderByDescending(m => m.FinalizedAt)
            .FirstOrDefault();

        var newMinutes = new Minutes
        {
            SeriesId = request.SeriesId,
            PreviousMinutesId = latestMinutes?.Id,
            ScheduledFor = request.ScheduledFor
        };

        // Carry forward pinned global note
        if (lastFinalized?.GlobalNotePinned == true)
            newMinutes.GlobalNote = lastFinalized.GlobalNote;
        else
            newMinutes.GlobalNote = request.GlobalNote;

        db.Minutes.Add(newMinutes);

        // Carry-forward: recurring open topics from last finalized Minutes
        if (lastFinalized is not null)
        {
            var recurringOpenTopics = lastFinalized.Topics
                .Where(t => t.Type == TopicType.Recurring && t.IsOpen)
                .OrderBy(t => t.SortOrder)
                .ToList();

            foreach (var sourceTopic in recurringOpenTopics)
            {
                var newTopic = new Topic
                {
                    MinutesId = newMinutes.Id,
                    SourceTopicId = sourceTopic.Id,
                    Title = sourceTopic.Title,
                    Type = TopicType.Recurring,
                    IsOpen = true,
                    IsSkipped = false,
                    SortOrder = sourceTopic.SortOrder,
                    ResponsibleId = sourceTopic.ResponsibleId
                };

                db.Topics.Add(newTopic);

                // Carry forward open action items on this topic
                var openItems = sourceTopic.ActionItems
                    .Where(a => a.Status == ActionItemStatus.Open ||
                                a.Status == ActionItemStatus.Deferred)
                    .ToList();

                foreach (var sourceItem in openItems)
                {
                    // Mark the original as Deferred — it has been carried forward
                    sourceItem.MarkDeferredByCarryForward();

                    // Create the carried-forward copy
                    var newItem = new ActionItem
                    {
                        TopicId = newTopic.Id,
                        SourceActionItemId = sourceItem.Id,
                        Title = sourceItem.Title,
                        ResponsibleId = sourceItem.ResponsibleId,
                        DueDate = sourceItem.DueDate,
                        Priority = sourceItem.Priority,
                        IsRecurring = sourceItem.IsRecurring,
                        AssignedInAbsentia = false, // Reset — attendance for new meeting not yet known
                        CreatedBy = currentUser.UserId
                    };

                    db.ActionItems.Add(newItem);

                    // Carry forward labels
                    foreach (var label in sourceItem.ActionItemLabels)
                    {
                        db.ActionItemLabels.Add(new ActionItemLabel
                        {
                            ActionItemId = newItem.Id,
                            LabelId = label.LabelId
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var result = await MinutesEndpoints.LoadMinutesDetailAsync(newMinutes.Id, request.SeriesId, db, ct);
        return Results.Created($"/minutes/{newMinutes.Id}", result);
    }

    // GET /minutes/{minutesId}
    private static async Task<IResult> GetMinutes(
        Guid minutesId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        SeriesEndpoints.EnsureParticipant(minutes.Series!, currentUser.UserId);

        var result = await MinutesEndpoints.LoadMinutesDetailAsync(minutesId, minutes.SeriesId, db, ct);
        return Results.Ok(result);
    }

    // PATCH /minutes/{minutesId}
    private static async Task<IResult> UpdateMinutes(
        Guid minutesId,
        UpdateMinutesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        var participant = minutes.Series!.Participants
            .FirstOrDefault(p => p.UserId == currentUser.UserId);

        if (participant is null || participant.Role == ParticipantRole.Informed)
            throw new ForbiddenException("Only moderators and invited participants may update Minutes.");

        minutes.EnsureEditable();

        if (request.ScheduledFor.HasValue) minutes.ScheduledFor = request.ScheduledFor.Value;
        if (request.GlobalNote is not null) minutes.GlobalNote = request.GlobalNote;
        if (request.GlobalNotePinned.HasValue) minutes.GlobalNotePinned = request.GlobalNotePinned.Value;

        await db.SaveChangesAsync(ct);

        var result = await MinutesEndpoints.LoadMinutesDetailAsync(minutesId, minutes.SeriesId, db, ct);
        return Results.Ok(result);
    }

    // POST /minutes/{minutesId}/finalize
    private static async Task<IResult> FinalizeMinutes(
        Guid minutesId,
        FinalizeMinutesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        SeriesEndpoints.EnsureModerator(minutes.Series!, currentUser.UserId);

        minutes.Finalize(currentUser.UserId);

        await db.SaveChangesAsync(ct);

        // Notifications are fire-and-forget in v1 — logged but don't affect the response
        // TODO: wire up INotificationService when email is implemented
        if (request.NotifyResponsibles || request.NotifyAll)
        {
            // Placeholder: log intent
        }

        var result = await MinutesEndpoints.LoadMinutesDetailAsync(minutesId, minutes.SeriesId, db, ct);
        return Results.Ok(result);
    }

    // POST /minutes/{minutesId}/abandon
    private static async Task<IResult> AbandonMinutes(
        Guid minutesId,
        AbandonMinutesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        SeriesEndpoints.EnsureModerator(minutes.Series!, currentUser.UserId);

        minutes.Abandon(currentUser.UserId, request.Note);

        // Atomically abandon all open/deferred action items beneath this Minutes
        var openItems = await db.ActionItems
            .Where(a => a.Topic!.MinutesId == minutesId &&
                        (a.Status == ActionItemStatus.Open ||
                         a.Status == ActionItemStatus.Deferred))
            .ToListAsync(ct);

        foreach (var item in openItems)
            item.AbandonBySystem();

        await db.SaveChangesAsync(ct);

        var result = await MinutesEndpoints.LoadMinutesDetailAsync(minutesId, minutes.SeriesId, db, ct);
        return Results.Ok(result);
    }

    // GET /minutes/{minutesId}/attendees
    private static async Task<IResult> ListAttendees(
        Guid minutesId,
        DocketDbContext db,
        CancellationToken ct)
    {
        // Verify that the Minutes exists — if not, return 404 instead of empty list
        var exists = await db.Minutes.AnyAsync(m => m.Id == minutesId, ct);
        if (!exists)
            throw new NotFoundException(nameof(Minutes), minutesId);

        var attendees = await db.MinutesAttendees
            .Include(a => a.User)
            .Where(a => a.MinutesId == minutesId)
            .ToListAsync(ct);

        return Results.Ok(attendees.Select(a =>
            new AttendeeResponse(a.UserId, UserResponse.From(a.User!))));
    }

    // POST /minutes/{minutesId}/attendees
    private static async Task<IResult> AddAttendee(
        Guid minutesId,
        AddAttendeeRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        minutes.EnsureEditable();

        var participant = minutes.Series!.Participants
            .FirstOrDefault(p => p.UserId == request.UserId);

        if (participant != null)
            throw new DuplicateAttendeeException(request.UserId, minutesId);

        //if (participant.Role == ParticipantRole.Informed)
        //    throw new InformedAttendeeException();

        if (minutes.Attendees.Any(a => a.UserId == request.UserId))
            throw new DuplicateAttendeeException(request.UserId, minutesId);

        var attendee = new MinutesAttendee
        {
            MinutesId = minutesId,
            UserId = request.UserId
        };

        db.MinutesAttendees.Add(attendee);
        await db.SaveChangesAsync(ct);

        await db.Entry(attendee).Reference(a => a.User).LoadAsync(ct);

        return Results.Created(
            $"/minutes/{minutesId}/attendees/{request.UserId}",
            new AttendeeResponse(attendee.UserId, UserResponse.From(attendee.User!)));
    }

    // DELETE /minutes/{minutesId}/attendees/{userId}
    private static async Task<IResult> RemoveAttendee(
        Guid minutesId,
        Guid userId,
        DocketDbContext db,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        minutes.EnsureEditable();

        var attendee = await db.MinutesAttendees
            .FirstOrDefaultAsync(a => a.MinutesId == minutesId && a.UserId == userId, ct)
            ?? throw new NotFoundException("MinutesAttendee", userId);

        db.MinutesAttendees.Remove(attendee);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static async Task<MinutesDetailResponse> LoadMinutesDetailAsync(
        Guid minutesId,
        Guid seriesId,
        DocketDbContext db,
        CancellationToken ct)
    {
        var minutes = await db.Minutes
            .Include(m => m.Attendees).ThenInclude(a => a.User)
            .Include(m => m.Topics).ThenInclude(t => t.InfoItems)
            .Include(m => m.Topics).ThenInclude(t => t.ActionItems)
                .ThenInclude(a => a.Notes)
            .Include(m => m.Topics).ThenInclude(t => t.ActionItems)
                .ThenInclude(a => a.ActionItemLabels).ThenInclude(al => al.Label)
            .Include(m => m.Topics).ThenInclude(t => t.TopicLabels).ThenInclude(tl => tl.Label)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
            ?? throw new NotFoundException(nameof(Minutes), minutesId);

        // Derive absent participants
        var attendeeIds = minutes.Attendees.Select(a => a.UserId).ToHashSet();
        var expectedParticipants = await db.SeriesParticipants
            .Include(p => p.User)
            .Where(p => p.SeriesId == seriesId &&
                        (p.Role == ParticipantRole.Moderator || p.Role == ParticipantRole.Invited))
            .ToListAsync(ct);

        var absent = expectedParticipants
            .Where(p => !attendeeIds.Contains(p.UserId))
            .Select(p => UserResponse.From(p.User!));

        return new MinutesDetailResponse(
            minutes.Id,
            minutes.SeriesId,
            minutes.PreviousMinutesId,
            minutes.ScheduledFor,
            minutes.Status.ToString(),
            minutes.Version,
            minutes.FinalizedAt,
            minutes.FinalizedBy,
            minutes.AbandonedAt,
            minutes.AbandonedBy,
            minutes.AbandonmentNote,
            minutes.GlobalNote,
            minutes.GlobalNotePinned,
            minutes.Topics.OrderBy(t => t.SortOrder).Select(TopicResponse.From),
            minutes.Attendees.Select(a => new AttendeeResponse(a.UserId, UserResponse.From(a.User!))),
            absent,
            minutes.CreatedAt,
            minutes.UpdatedAt);
    }
}

public record AddAttendeeRequest(Guid UserId);
