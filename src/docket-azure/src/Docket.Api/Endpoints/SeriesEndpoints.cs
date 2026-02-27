using Docket.Api.Services;
using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Docket.Domain.Exceptions;
using Docket.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Docket.Api.Endpoints;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public record SeriesResponse(
    Guid Id,
    string Name,
    string? Project,
    string Status,
    string? ExternalCalendarId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static SeriesResponse From(MeetingSeries s) => new(
        s.Id, s.Name, s.Project, s.Status.ToString(),
        s.ExternalCalendarId, s.CreatedBy, s.CreatedAt, s.UpdatedAt);
}

public record CreateSeriesRequest(
    string Name,
    string? Project,
    string? ExternalCalendarId);

public record UpdateSeriesRequest(
    string? Name,
    string? Project,
    string? ExternalCalendarId);

public record ParticipantResponse(
    Guid Id,
    Guid SeriesId,
    Guid UserId,
    UserResponse User,
    string Role,
    DateTimeOffset AddedAt)
{
    public static ParticipantResponse From(SeriesParticipant p) => new(
        p.Id, p.SeriesId, p.UserId,
        UserResponse.From(p.User!),
        p.Role.ToString(),
        p.AddedAt);
}

public record AddParticipantRequest(Guid UserId, string Role);
public record UpdateParticipantRoleRequest(string Role);

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

public static class SeriesEndpoints
{
    public static void MapSeriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/series").WithTags("series");

        group.MapGet("/", ListSeries)
            .WithName("ListSeries")
            .WithSummary("List all series the caller participates in");

        group.MapPost("/", CreateSeries)
            .WithName("CreateSeries")
            .WithSummary("Create a new MeetingSeries");

        group.MapGet("/{seriesId:guid}", GetSeries)
            .WithName("GetSeries")
            .WithSummary("Get a MeetingSeries by ID");

        group.MapPatch("/{seriesId:guid}", UpdateSeries)
            .WithName("UpdateSeries")
            .WithSummary("Update series name, project tag, or external calendar ID");

        group.MapPost("/{seriesId:guid}/archive", ArchiveSeries)
            .WithName("ArchiveSeries")
            .WithSummary("Archive a MeetingSeries — irreversible");

        group.MapGet("/{seriesId:guid}/participants", ListParticipants)
            .WithName("ListParticipants")
            .WithSummary("List all participants in a series");

        group.MapPost("/{seriesId:guid}/participants", AddParticipant)
            .WithName("AddParticipant")
            .WithSummary("Add a participant to a series");

        group.MapPatch("/{seriesId:guid}/participants/{userId:guid}", UpdateParticipantRole)
            .WithName("UpdateParticipantRole")
            .WithSummary("Change a participant's role");

        group.MapDelete("/{seriesId:guid}/participants/{userId:guid}", RemoveParticipant)
            .WithName("RemoveParticipant")
            .WithSummary("Remove a participant from a series");

        group.MapGet("/{seriesId:guid}/minutes", ListMinutesForSeries)
            .WithName("ListMinutesForSeries")
            .WithSummary("List all Minutes for a series in chain order");
    }

    // GET /series
    private static async Task<IResult> ListSeries(
        string? status,
        string? project,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var query = db.MeetingSeries
            .Where(s => s.Participants.Any(p => p.UserId == currentUser.UserId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<SeriesStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(s => s.Status == parsedStatus);
        }
        else
        {
            // Default: active only
            query = query.Where(s => s.Status == SeriesStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(project))
            query = query.Where(s => s.Project == project);

        var series = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return Results.Ok(series.Select(SeriesResponse.From));
    }

    // POST /series
    private static async Task<IResult> CreateSeries(
        CreateSeriesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (request.Name == null || string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentMissingException("Series Name");

        var series = new MeetingSeries
        {
            Name = request.Name.Trim(),
            Project = request.Project?.Trim(),
            ExternalCalendarId = request.ExternalCalendarId?.Trim(),
            CreatedBy = currentUser.UserId
        };

        db.MeetingSeries.Add(series);

        // Caller becomes the initial moderator
        db.SeriesParticipants.Add(new SeriesParticipant
        {
            SeriesId = series.Id,
            UserId = currentUser.UserId,
            Role = ParticipantRole.Moderator,
            AddedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);

        return Results.Created($"/series/{series.Id}", SeriesResponse.From(series));
    }

    // GET /series/{seriesId}
    private static async Task<IResult> GetSeries(
        Guid seriesId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureParticipant(series, currentUser.UserId);

        return Results.Ok(SeriesResponse.From(series));
    }

    // PATCH /series/{seriesId}
    private static async Task<IResult> UpdateSeries(
        Guid seriesId,
        UpdateSeriesRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureModerator(series, currentUser.UserId);

        if (series.Status == SeriesStatus.Archived)
            throw new SeriesArchivedException(seriesId);

        if (request.Name is not null) series.Name = request.Name.Trim();
        if (request.Project is not null) series.Project = request.Project.Trim();
        if (request.ExternalCalendarId is not null)
            series.ExternalCalendarId = request.ExternalCalendarId.Trim();

        await db.SaveChangesAsync(ct);
        return Results.Ok(SeriesResponse.From(series));
    }

    // POST /series/{seriesId}/archive
    private static async Task<IResult> ArchiveSeries(
        Guid seriesId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureModerator(series, currentUser.UserId);

        if (series.Status == SeriesStatus.Archived)
            throw new InvalidStatusTransitionException("Archived", "Archived");

        // Gate: no unresolved drafts anywhere in any Minutes chain for this series
        await EnsureNoUnresolvedDraftsAsync(seriesId, db, ct);

        // Transition
        series.Status = SeriesStatus.Archived;

        // Atomically abandon all open/deferred action items across all Minutes
        var openItems = await db.ActionItems
            .Where(a => a.Topic!.Minutes!.SeriesId == seriesId &&
                        (a.Status == ActionItemStatus.Open ||
                         a.Status == ActionItemStatus.Deferred))
            .ToListAsync(ct);

        foreach (var item in openItems)
            item.AbandonBySystem();

        await db.SaveChangesAsync(ct);
        return Results.Ok(SeriesResponse.From(series));
    }

    // GET /series/{seriesId}/participants
    private static async Task<IResult> ListParticipants(
        Guid seriesId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureParticipant(series, currentUser.UserId);

        return Results.Ok(series.Participants.Select(ParticipantResponse.From));
    }

    // POST /series/{seriesId}/participants
    private static async Task<IResult> AddParticipant(
        Guid seriesId,
        AddParticipantRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureModerator(series, currentUser.UserId);

        if (!Enum.TryParse<ParticipantRole>(request.Role, ignoreCase: true, out var role))
            throw new InvalidStatusTransitionException("unknown", request.Role);

        // Cannot add a second moderator via this endpoint
        if (role == ParticipantRole.Moderator)
            throw new SingleModeratorException();

        // User must exist
        var userExists = await db.Users.AnyAsync(u => u.Id == request.UserId, ct);
        if (!userExists)
            throw new NotFoundException(nameof(User), request.UserId);

        if (series.Participants.Any(p => p.UserId == request.UserId))
            throw new DuplicateParticipantException(request.UserId, seriesId);

        var participant = new SeriesParticipant
        {
            SeriesId = seriesId,
            UserId = request.UserId,
            Role = role,
            AddedAt = DateTimeOffset.UtcNow
        };

        db.SeriesParticipants.Add(participant);
        await db.SaveChangesAsync(ct);

        // Reload with user for response
        await db.Entry(participant).Reference(p => p.User).LoadAsync(ct);

        return Results.Created(
            $"/series/{seriesId}/participants/{request.UserId}",
            ParticipantResponse.From(participant));
    }

    // PATCH /series/{seriesId}/participants/{userId}
    private static async Task<IResult> UpdateParticipantRole(
        Guid seriesId,
        Guid userId,
        UpdateParticipantRoleRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureModerator(series, currentUser.UserId);

        if (!Enum.TryParse<ParticipantRole>(request.Role, ignoreCase: true, out var newRole))
            throw new InvalidStatusTransitionException("unknown", request.Role);

        var target = series.Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new NotFoundException(nameof(SeriesParticipant), userId);

        // No-op if already the right role
        if (target.Role == newRole)
            return Results.Ok(ParticipantResponse.From(target));

        if (newRole == ParticipantRole.Moderator)
        {
            // Transfer moderator: demote current moderator to Invited, atomically
            var currentModerator = series.Participants.First(p => p.Role == ParticipantRole.Moderator);
            currentModerator.Role = ParticipantRole.Invited;
            target.Role = ParticipantRole.Moderator;
        }
        else
        {
            // Guard: cannot demote the only moderator
            if (target.Role == ParticipantRole.Moderator)
                throw new SingleModeratorException();
            target.Role = newRole;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ParticipantResponse.From(target));
    }

    // DELETE /series/{seriesId}/participants/{userId}
    private static async Task<IResult> RemoveParticipant(
        Guid seriesId,
        Guid userId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureModerator(series, currentUser.UserId);

        var target = series.Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new NotFoundException(nameof(SeriesParticipant), userId);

        if (target.Role == ParticipantRole.Moderator)
            throw new SingleModeratorException();

        db.SeriesParticipants.Remove(target);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // GET /series/{seriesId}/minutes
    private static async Task<IResult> ListMinutesForSeries(
        Guid seriesId,
        string? status,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var series = await db.MeetingSeries
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw new NotFoundException(nameof(MeetingSeries), seriesId);

        EnsureParticipant(series, currentUser.UserId);

        // Load the full chain for this series
        var allMinutes = await db.Minutes
            .Where(m => m.SeriesId == seriesId)
            .Include(m => m.Topics)
                .ThenInclude(t => t.ActionItems)
            .ToListAsync(ct);

        // Walk the linked list from tail back to head, then reverse
        // This guarantees chain order regardless of creation order
        var chainOrdered = BuildChain(allMinutes);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<MinutesStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            chainOrdered = chainOrdered.Where(m => m.Status == parsedStatus).ToList();
        }

        return Results.Ok(chainOrdered.Select(MinutesSummaryResponse.From));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static void EnsureParticipant(MeetingSeries series, Guid userId)
    {
        if (!series.Participants.Any(p => p.UserId == userId))
            throw new ForbiddenException(
                $"You are not a participant in series {series.Id}.");
    }

    internal static void EnsureModerator(MeetingSeries series, Guid userId)
    {
        var participant = series.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant?.Role != ParticipantRole.Moderator)
            throw new ForbiddenException(
                $"Only the series moderator may perform this action.");
    }

    internal static async Task EnsureNoUnresolvedDraftsAsync(
        Guid seriesId, DocketDbContext db, CancellationToken ct)
    {
        var drafts = await db.Minutes
            .Where(m => m.SeriesId == seriesId && m.Status == MinutesStatus.Draft)
            .Select(m => new UnresolvedDraftInfo(m.Id, m.ScheduledFor))
            .ToListAsync(ct);

        if (drafts.Count > 0)
            throw new UnresolvedDraftsException(drafts);
    }

    private static List<Minutes> BuildChain(List<Minutes> all)
    {
        if (all.Count == 0) return [];

        // Build lookup by id
        var byId = all.ToDictionary(m => m.Id);

        // Find the tail: Minutes with no successor
        var hasSuccessor = all
            .Where(m => m.PreviousMinutesId.HasValue)
            .Select(m => m.PreviousMinutesId!.Value)
            .ToHashSet();

        var tails = all.Where(m => !hasSuccessor.Contains(m.Id)).ToList();

        // Walk backwards from each tail to build the chain
        // (In normal operation there is one tail; multiple tails indicate orphaned chains)
        var chain = new List<Minutes>();
        foreach (var tail in tails)
        {
            var segment = new List<Minutes>();
            var current = tail;
            while (current != null)
            {
                segment.Add(current);
                current = current.PreviousMinutesId.HasValue &&
                           byId.TryGetValue(current.PreviousMinutesId.Value, out var prev)
                    ? prev : null;
            }
            segment.Reverse();
            chain.AddRange(segment);
        }

        return chain;
    }
}
