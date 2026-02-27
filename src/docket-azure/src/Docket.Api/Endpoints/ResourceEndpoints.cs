using Docket.Api.Services;
using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Docket.Domain.Exceptions;
using Docket.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Docket.Api.Endpoints;

// ---------------------------------------------------------------------------
// DTOs shared across topics, info items, action items
// ---------------------------------------------------------------------------

public record LabelResponse(
    Guid Id,
    string Name,
    string Category,
    string? Color,
    bool IsSystem,
    DateTimeOffset CreatedAt)
{
    public static LabelResponse From(Label l) => new(
        l.Id, l.Name, l.Category.ToString(), l.Color, l.IsSystem, l.CreatedAt);
}

public record TopicResponse(
    Guid Id,
    Guid MinutesId,
    Guid? SourceTopicId,
    string Title,
    string Type,
    bool IsOpen,
    bool IsSkipped,
    int SortOrder,
    Guid? ResponsibleId,
    IEnumerable<LabelResponse> Labels,
    IEnumerable<InfoItemResponse> InfoItems,
    IEnumerable<ActionItemResponse> ActionItems,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static TopicResponse From(Topic t) => new(
        t.Id, t.MinutesId, t.SourceTopicId, t.Title, t.Type.ToString(),
        t.IsOpen, t.IsSkipped, t.SortOrder, t.ResponsibleId,
        t.TopicLabels.Select(tl => LabelResponse.From(tl.Label!)),
        t.InfoItems.OrderBy(i => i.CreatedAt).Select(InfoItemResponse.From),
        t.ActionItems.OrderBy(a => a.CreatedAt).Select(ActionItemResponse.From),
        t.CreatedAt, t.UpdatedAt);
}

public record InfoItemResponse(
    Guid Id,
    Guid TopicId,
    string Text,
    DateOnly? PinnedDate,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static InfoItemResponse From(InfoItem i) => new(
        i.Id, i.TopicId, i.Text, i.PinnedDate, i.CreatedBy, i.CreatedAt, i.UpdatedAt);
}

public record ActionItemNoteResponse(
    Guid Id,
    Guid ActionItemId,
    string Text,
    string Phase,
    Guid AuthorId,
    UserResponse? Author,
    DateTimeOffset CreatedAt)
{
    public static ActionItemNoteResponse From(ActionItemNote n) => new(
        n.Id, n.ActionItemId, n.Text, n.Phase.ToString(),
        n.AuthorId, n.Author is not null ? UserResponse.From(n.Author) : null,
        n.CreatedAt);
}

public record ActionItemResponse(
    Guid Id,
    Guid TopicId,
    Guid? SourceActionItemId,
    string Title,
    Guid ResponsibleId,
    DateOnly? DueDate,
    int Priority,
    string Status,
    bool IsRecurring,
    bool AssignedInAbsentia,
    IEnumerable<LabelResponse> Labels,
    IEnumerable<ActionItemNoteResponse> Notes,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ActionItemResponse From(ActionItem a) => new(
        a.Id, a.TopicId, a.SourceActionItemId, a.Title, a.ResponsibleId,
        a.DueDate, a.Priority, a.Status.ToString(), a.IsRecurring, a.AssignedInAbsentia,
        a.ActionItemLabels.Select(al => LabelResponse.From(al.Label!)),
        a.Notes.OrderBy(n => n.CreatedAt).Select(ActionItemNoteResponse.From),
        a.CreatedBy, a.CreatedAt, a.UpdatedAt);
}

public record ActionItemHistoryNodeResponse(
Guid Id,
Guid TopicId,
string TopicTitle,
Guid MinutesId,
DateTimeOffset MinutesScheduledFor,
string MinutesStatus,
Guid SeriesId,
string SeriesName,
Guid? SourceActionItemId,
string Title,
Guid ResponsibleId,
DateOnly? DueDate,
int Priority,
string Status,
bool IsRecurring,
bool AssignedInAbsentia,
IEnumerable<LabelResponse> Labels,
IEnumerable<ActionItemNoteResponse> Notes,
Guid CreatedBy,
DateTimeOffset CreatedAt,
DateTimeOffset UpdatedAt);


// ---------------------------------------------------------------------------
// TOPICS
// ---------------------------------------------------------------------------

public record CreateTopicRequest(
    string Title,
    string? Type,
    Guid? ResponsibleId,
    int? SortOrder);

public record UpdateTopicRequest(
    string? Title,
    string? Type,
    bool? IsOpen,
    bool? IsSkipped,
    Guid? ResponsibleId,
    int? SortOrder);

public static class TopicEndpoints
{
    public static void MapTopicEndpoints(this IEndpointRouteBuilder app)
    {
        var minutesGroup = app.MapGroup("/minutes/{minutesId:guid}/topics").WithTags("topics");
        minutesGroup.MapGet("/", ListTopics).WithName("ListTopics");
        minutesGroup.MapPost("/", CreateTopic).WithName("CreateTopic");

        var topicGroup = app.MapGroup("/topics").WithTags("topics");
        topicGroup.MapGet("/{topicId:guid}", GetTopic).WithName("GetTopic");
        topicGroup.MapPatch("/{topicId:guid}", UpdateTopic).WithName("UpdateTopic");
        topicGroup.MapDelete("/{topicId:guid}", DeleteTopic).WithName("DeleteTopic");
    }

    private static async Task<IResult> ListTopics(
        Guid minutesId,
        bool? includeOpen,
        bool? includeClosed,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(minutesId, db, ct);
        SeriesEndpoints.EnsureParticipant(minutes.Series!, currentUser.UserId);

        var topics = await db.Topics
            .Where(t => t.MinutesId == minutesId)
            .Include(t => t.InfoItems)
            .Include(t => t.ActionItems).ThenInclude(a => a.Notes)
            .Include(t => t.ActionItems).ThenInclude(a => a.ActionItemLabels).ThenInclude(al => al.Label)
            .Include(t => t.TopicLabels).ThenInclude(tl => tl.Label)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

        var showOpen = includeOpen ?? true;
        var showClosed = includeClosed ?? true;

        var filtered = topics.Where(t =>
            (t.IsOpen && showOpen) || (!t.IsOpen && showClosed));

        return Results.Ok(filtered.Select(TopicResponse.From));
    }

    private static async Task<IResult> CreateTopic(
        Guid minutesId,
        CreateTopicRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if(request.Title == null || string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentMissingException("Topic Title");

        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(minutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        var maxSortOrder = await db.Topics
            .Where(t => t.MinutesId == minutesId)
            .Select(t => (int?)t.SortOrder)
            .MaxAsync(ct) ?? 0;

        var topic = new Topic
        {
            MinutesId = minutesId,
            Title = request.Title.Trim(),
            Type = ParseTopicType(request.Type),
            SortOrder = request.SortOrder ?? maxSortOrder + 10,
            ResponsibleId = request.ResponsibleId
        };

        db.Topics.Add(topic);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/topics/{topic.Id}", TopicResponse.From(topic));
    }

    private static async Task<IResult> GetTopic(
        Guid topicId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var topic = await EndpointHelpers.LoadTopicFullAsync(topicId, db, ct);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic.MinutesId, db, ct);
        SeriesEndpoints.EnsureParticipant(minutes.Series!, currentUser.UserId);

        return Results.Ok(TopicResponse.From(topic));
    }

    private static async Task<IResult> UpdateTopic(
        Guid topicId,
        UpdateTopicRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var topic = await EndpointHelpers.LoadTopicFullAsync(topicId, db, ct);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        if (request.Title is not null) topic.Title = request.Title.Trim();
        if (request.Type is not null) topic.Type = ParseTopicType(request.Type);
        if (request.ResponsibleId is not null) topic.ResponsibleId = request.ResponsibleId;
        if (request.SortOrder.HasValue) topic.SortOrder = request.SortOrder.Value;

        if (request.IsOpen.HasValue)
        {
            topic.IsOpen = request.IsOpen.Value;
            if (!topic.IsOpen) topic.IsSkipped = false; // Closing clears skipped
        }

        if (request.IsSkipped.HasValue)
        {
            if (request.IsSkipped.Value && !topic.IsOpen)
                throw new InvalidStatusTransitionException("closed", "skipped");
            topic.IsSkipped = request.IsSkipped.Value;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(TopicResponse.From(topic));
    }

    private static async Task<IResult> DeleteTopic(
        Guid topicId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var topic = await db.Topics.FindAsync([topicId], ct)
            ?? throw new NotFoundException(nameof(Topic), topicId);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic.MinutesId, db, ct);
        SeriesEndpoints.EnsureModerator(minutes.Series!, currentUser.UserId);
        minutes.EnsureEditable();

        db.Topics.Remove(topic);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static TopicType ParseTopicType(string? type) =>
        type is not null && Enum.TryParse<TopicType>(type, ignoreCase: true, out var t)
            ? t : TopicType.Adhoc;
}

// ---------------------------------------------------------------------------
// INFO ITEMS
// ---------------------------------------------------------------------------

public record CreateInfoItemRequest(string Text, DateOnly? PinnedDate);
public record UpdateInfoItemRequest(string? Text, DateOnly? PinnedDate);

public static class InfoItemEndpoints
{
    public static void MapInfoItemEndpoints(this IEndpointRouteBuilder app)
    {
        var topicGroup = app.MapGroup("/topics/{topicId:guid}/info-items").WithTags("info-items");
        topicGroup.MapPost("/", CreateInfoItem).WithName("CreateInfoItem");

        var itemGroup = app.MapGroup("/info-items").WithTags("info-items");
        itemGroup.MapPatch("/{infoItemId:guid}", UpdateInfoItem).WithName("UpdateInfoItem");
        itemGroup.MapDelete("/{infoItemId:guid}", DeleteInfoItem).WithName("DeleteInfoItem");
    }

    private static async Task<IResult> CreateInfoItem(
        Guid topicId,
        CreateInfoItemRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if(request.Text == null || string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentMissingException("Info Item Text");   

        var topic = await db.Topics.FindAsync([topicId], ct)
            ?? throw new NotFoundException(nameof(Topic), topicId);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        var item = new InfoItem
        {
            TopicId = topicId,
            Text = request.Text,
            PinnedDate = request.PinnedDate,
            CreatedBy = currentUser.UserId
        };

        db.InfoItems.Add(item);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/info-items/{item.Id}", InfoItemResponse.From(item));
    }

    private static async Task<IResult> UpdateInfoItem(
        Guid infoItemId,
        UpdateInfoItemRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await db.InfoItems.FindAsync([infoItemId], ct)
            ?? throw new NotFoundException(nameof(InfoItem), infoItemId);
        var topic = await db.Topics.FindAsync([item.TopicId], ct)!;
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic!.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        if (request.Text is not null) item.Text = request.Text;
        if (request.PinnedDate is not null) item.PinnedDate = request.PinnedDate;

        await db.SaveChangesAsync(ct);
        return Results.Ok(InfoItemResponse.From(item));
    }

    private static async Task<IResult> DeleteInfoItem(
        Guid infoItemId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await db.InfoItems.FindAsync([infoItemId], ct)
            ?? throw new NotFoundException(nameof(InfoItem), infoItemId);
        var topic = await db.Topics.FindAsync([item.TopicId], ct)!;
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic!.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        db.InfoItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

// ---------------------------------------------------------------------------
// ACTION ITEMS
// ---------------------------------------------------------------------------

public record CreateActionItemRequest(
    string Title,
    Guid ResponsibleId,
    DateOnly? DueDate,
    int Priority = 3,
    bool IsRecurring = false,
    bool? AssignedInAbsentia = null);

public record UpdateActionItemRequest(
    string? Title,
    DateOnly? DueDate,
    int? Priority,
    string? Status,
    bool? IsRecurring);

public record AppendNoteRequest(string Text);
public record ApplyLabelRequest(Guid LabelId);

public static class ActionItemEndpoints
{
    public static void MapActionItemEndpoints(this IEndpointRouteBuilder app)
    {
        var topicGroup = app.MapGroup("/topics/{topicId:guid}/action-items").WithTags("action-items");
        topicGroup.MapPost("/", CreateActionItem).WithName("CreateActionItem");

        var itemGroup = app.MapGroup("/action-items").WithTags("action-items");
        itemGroup.MapGet("/{actionItemId:guid}", GetActionItem).WithName("GetActionItem");
        itemGroup.MapPatch("/{actionItemId:guid}", UpdateActionItem).WithName("UpdateActionItem");
        itemGroup.MapGet("/{actionItemId:guid}/history", GetActionItemHistory).WithName("GetActionItemHistory");
        itemGroup.MapPost("/{actionItemId:guid}/notes", AppendNote).WithName("AppendNote");
        itemGroup.MapPost("/{actionItemId:guid}/labels", ApplyLabel).WithName("ApplyLabelToActionItem");
        itemGroup.MapDelete("/{actionItemId:guid}/labels/{labelId:guid}", RemoveLabel).WithName("RemoveLabelFromActionItem");
    }

    private static async Task<IResult> CreateActionItem(
        Guid topicId,
        CreateActionItemRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (request.Title == null || string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentMissingException("Action Item Title");

        var topic = await db.Topics.FindAsync([topicId], ct)
            ?? throw new NotFoundException(nameof(Topic), topicId);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(topic.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        // Responsible must be moderator or invited — not informed
        var responsible = minutes.Series!.Participants
            .FirstOrDefault(p => p.UserId == request.ResponsibleId)
            ?? throw new NotFoundException(nameof(User), request.ResponsibleId);

        if (responsible.Role == ParticipantRole.Informed)
            throw new InformedOwnerException();

        // Derive assignedInAbsentia if not explicitly set
        bool inAbsentia;
        if (request.AssignedInAbsentia.HasValue)
        {
            inAbsentia = request.AssignedInAbsentia.Value;
        }
        else
        {
            inAbsentia = !await db.MinutesAttendees
                .AnyAsync(a => a.MinutesId == topic.MinutesId &&
                               a.UserId == request.ResponsibleId, ct);
        }

        var item = new ActionItem
        {
            TopicId = topicId,
            Title = request.Title.Trim(),
            ResponsibleId = request.ResponsibleId,
            DueDate = request.DueDate,
            Priority = request.Priority,
            IsRecurring = request.IsRecurring,
            AssignedInAbsentia = inAbsentia,
            CreatedBy = currentUser.UserId
        };

        db.ActionItems.Add(item);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/action-items/{item.Id}",
            ActionItemResponse.From(await LoadActionItemAsync(item.Id, db, ct)));
    }

    private static async Task<IResult> GetActionItem(
        Guid actionItemId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await LoadActionItemAsync(actionItemId, db, ct);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(item.Topic!.MinutesId, db, ct);
        SeriesEndpoints.EnsureParticipant(minutes.Series!, currentUser.UserId);

        return Results.Ok(ActionItemResponse.From(item));
    }

    private static async Task<IResult> UpdateActionItem(
        Guid actionItemId,
        UpdateActionItemRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await LoadActionItemAsync(actionItemId, db, ct);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(item.Topic!.MinutesId, db, ct);
        EndpointHelpers.EnsureModeratorOrInvited(minutes, currentUser.UserId);
        minutes.EnsureEditable();

        if (request.Title is not null) item.Title = request.Title.Trim();
        if (request.DueDate is not null) item.DueDate = request.DueDate;
        if (request.Priority.HasValue) item.Priority = request.Priority.Value;
        if (request.IsRecurring.HasValue) item.IsRecurring = request.IsRecurring.Value;

        if (request.Status is not null)
        {
            // Guard: Abandoned is system-set only
            if (request.Status.Equals("abandoned", StringComparison.OrdinalIgnoreCase))
                throw new InvalidStatusTransitionException(item.Status.ToString(), "Abandoned");

            if (request.Status.Equals("done", StringComparison.OrdinalIgnoreCase))
                item.MarkDone();
            else if (request.Status.Equals("deferred", StringComparison.OrdinalIgnoreCase))
                item.Defer();
            else if (request.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
                item.Reopen();
            else
                throw new InvalidStatusTransitionException(item.Status.ToString(), request.Status);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ActionItemResponse.From(await LoadActionItemAsync(actionItemId, db, ct)));
    }

    private static async Task<IResult> GetActionItemHistory(
        Guid actionItemId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await LoadActionItemAsync(actionItemId, db, ct);
        var minutes = await EndpointHelpers.LoadMinutesWithSeriesAsync(item.Topic!.MinutesId, db, ct);
        SeriesEndpoints.EnsureParticipant(minutes.Series!, currentUser.UserId);

        // Traverse backwards to find root
        var chain = new List<ActionItem>();
        var current = item;
        while (current is not null)
        {
            chain.Add(current);
            current = current.SourceActionItemId.HasValue
                ? await LoadActionItemAsync(current.SourceActionItemId.Value, db, ct)
                : null;
        }
        chain.Reverse();

        // Traverse forward from root to find all descendants
        var root = chain[0];
        var visited = new HashSet<Guid> { root.Id };

        var allItems = await db.ActionItems
            .Where(a => a.Topic!.MinutesId == item.Topic.MinutesId ||
                        a.SourceActionItemId != null)
            .Include(a => a.Notes).ThenInclude(n => n.Author)
            .Include(a => a.ActionItemLabels).ThenInclude(al => al.Label)
            .Include(a => a.Topic)
            .ToListAsync(ct);

        var bySource = allItems
            .Where(a => a.SourceActionItemId.HasValue)
            .GroupBy(a => a.SourceActionItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var queue = new Queue<Guid>();
        queue.Enqueue(root.Id);
        var orderedChain = new List<ActionItem> { root };

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (bySource.TryGetValue(parentId, out var children))
            {
                foreach (var child in children)
                {
                    if (visited.Add(child.Id))
                    {
                        orderedChain.Add(child);
                        queue.Enqueue(child.Id);
                    }
                }
            }
        }

        // Hydrate meeting context for each node — collect unique minutesIds
        var minutesIds = orderedChain
            .Select(a => a.Topic!.MinutesId)
            .Distinct()
            .ToList();

        var minutesMap = await db.Minutes
            .Include(m => m.Series)
            .Where(m => minutesIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        // Build enriched response
        var result = orderedChain.Select(a =>
        {
            var m = minutesMap[a.Topic!.MinutesId];
            return new ActionItemHistoryNodeResponse(
                a.Id,
                a.TopicId,
                a.Topic.Title,
                m.Id,
                m.ScheduledFor,
                m.Status.ToString(),
                m.SeriesId,
                m.Series!.Name,
                a.SourceActionItemId,
                a.Title,
                a.ResponsibleId,
                a.DueDate,
                a.Priority,
                a.Status.ToString(),
                a.IsRecurring,
                a.AssignedInAbsentia,
                a.ActionItemLabels.Select(al => LabelResponse.From(al.Label!)),
                a.Notes.OrderBy(n => n.CreatedAt).Select(ActionItemNoteResponse.From),
                a.CreatedBy,
                a.CreatedAt,
                a.UpdatedAt);
        });

        return Results.Ok(result);
    }
    private static async Task<IResult> AppendNote(
        Guid actionItemId,
        AppendNoteRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ArgumentMissingException("Note Text");

        var item = await db.ActionItems
            .Include(a => a.Topic).ThenInclude(t => t!.Minutes)
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new NotFoundException(nameof(ActionItem), actionItemId);

        // Determine phase based on parent Minutes status — server-set, never caller-set
        var minutesStatus = item.Topic!.Minutes!.Status;
        var phase = minutesStatus == MinutesStatus.Draft
            ? NotePhase.Meeting
            : NotePhase.PostMeeting;

        var note = new ActionItemNote
        {
            ActionItemId = actionItemId,
            Text = request.Text,
            Phase = phase,
            AuthorId = currentUser.UserId
        };

        db.ActionItemNotes.Add(note);
        await db.SaveChangesAsync(ct);

        await db.Entry(note).Reference(n => n.Author).LoadAsync(ct);

        return Results.Created(
            $"/action-items/{actionItemId}/notes/{note.Id}",
            ActionItemNoteResponse.From(note));
    }

    private static async Task<IResult> ApplyLabel(
        Guid actionItemId,
        ApplyLabelRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await db.ActionItems
            .Include(a => a.Topic).ThenInclude(t => t!.Minutes)
            .Include(a => a.ActionItemLabels)
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new NotFoundException(nameof(ActionItem), actionItemId);

        item.Topic!.Minutes!.EnsureEditable();

        if (item.ActionItemLabels.Any(al => al.LabelId == request.LabelId))
            throw new LabelAlreadyAppliedException(request.LabelId, actionItemId);

        var labelExists = await db.Labels.AnyAsync(l => l.Id == request.LabelId, ct);
        if (!labelExists)
            throw new NotFoundException(nameof(Label), request.LabelId);

        db.ActionItemLabels.Add(new ActionItemLabel
        {
            ActionItemId = actionItemId,
            LabelId = request.LabelId
        });

        await db.SaveChangesAsync(ct);
        return Results.Created($"/action-items/{actionItemId}/labels/{request.LabelId}", null);
    }

    private static async Task<IResult> RemoveLabel(
        Guid actionItemId,
        Guid labelId,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        var item = await db.ActionItems
            .Include(a => a.Topic).ThenInclude(t => t!.Minutes)
            .Include(a => a.ActionItemLabels)
            .FirstOrDefaultAsync(a => a.Id == actionItemId, ct)
            ?? throw new NotFoundException(nameof(ActionItem), actionItemId);

        item.Topic!.Minutes!.EnsureEditable();

        var junction = item.ActionItemLabels.FirstOrDefault(al => al.LabelId == labelId)
            ?? throw new NotFoundException("ActionItemLabel", labelId);

        db.ActionItemLabels.Remove(junction);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<ActionItem> LoadActionItemAsync(
        Guid id, DocketDbContext db, CancellationToken ct) =>
        await db.ActionItems
            .Include(a => a.Notes).ThenInclude(n => n.Author)
            .Include(a => a.ActionItemLabels).ThenInclude(al => al.Label)
            .Include(a => a.Topic)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(nameof(ActionItem), id);
}

// ---------------------------------------------------------------------------
// LABELS
// ---------------------------------------------------------------------------

public record CreateLabelRequest(string Name, string Category, string? Color);

public static class LabelEndpoints
{
    public static void MapLabelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/labels").WithTags("labels");
        group.MapGet("/", ListLabels).WithName("ListLabels");
        group.MapPost("/", CreateLabel).WithName("CreateLabel");
        group.MapDelete("/{labelId:guid}", DeleteLabel).WithName("DeleteLabel");
    }

    private static async Task<IResult> ListLabels(
        string? category,
        DocketDbContext db,
        CancellationToken ct)
    {
        var query = db.Labels.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<LabelCategory>(category, ignoreCase: true, out var cat))
        {
            query = query.Where(l => l.Category == cat);
        }

        var labels = await query.OrderBy(l => l.Name).ToListAsync(ct);
        return Results.Ok(labels.Select(LabelResponse.From));
    }

    private static async Task<IResult> CreateLabel(
        CreateLabelRequest request,
        DocketDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentMissingException("Label Name");

        if (!Enum.TryParse<LabelCategory>(request.Category, ignoreCase: true, out var category))
            throw new InvalidStatusTransitionException("unknown", request.Category);

        var exists = await db.Labels.AnyAsync(l => l.Name == request.Name, ct);
        if (exists)
            throw new DuplicateLabelException(request.Name);

        var label = new Label
        {
            Name = request.Name.Trim(),
            Category = category,
            Color = request.Color?.Trim(),
            IsSystem = false
        };

        db.Labels.Add(label);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/labels/{label.Id}", LabelResponse.From(label));
    }

    private static async Task<IResult> DeleteLabel(
        Guid labelId,
        DocketDbContext db,
        CancellationToken ct)
    {
        var label = await db.Labels.FindAsync([labelId], ct)
            ?? throw new NotFoundException(nameof(Label), labelId);

        if (label.IsSystem)
            throw new SystemLabelException();

        db.Labels.Remove(label);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

// ---------------------------------------------------------------------------
// Shared helpers (used across multiple endpoint files via internal visibility)
// ---------------------------------------------------------------------------

internal static class EndpointHelpers
{
    internal static async Task<Minutes> LoadMinutesWithSeriesAsync(
        Guid minutesId, DocketDbContext db, CancellationToken ct) =>
        await db.Minutes
            .Include(m => m.Series)
                .ThenInclude(s => s!.Participants)
            .FirstOrDefaultAsync(m => m.Id == minutesId, ct)
        ?? throw new NotFoundException(nameof(Minutes), minutesId);

    internal static void EnsureModeratorOrInvited(Minutes minutes, Guid userId)
    {
        var participant = minutes.Series!.Participants
            .FirstOrDefault(p => p.UserId == userId);

        if (participant is null || participant.Role == ParticipantRole.Informed)
            throw new ForbiddenException(
                "Only moderators and invited participants may perform this action.");
    }

    internal static async Task<Topic> LoadTopicFullAsync(
        Guid topicId, DocketDbContext db, CancellationToken ct) =>
        await db.Topics
            .Include(t => t.InfoItems)
            .Include(t => t.ActionItems).ThenInclude(a => a.Notes)
            .Include(t => t.ActionItems).ThenInclude(a => a.ActionItemLabels).ThenInclude(al => al.Label)
            .Include(t => t.TopicLabels).ThenInclude(tl => tl.Label)
            .FirstOrDefaultAsync(t => t.Id == topicId, ct)
        ?? throw new NotFoundException(nameof(Topic), topicId);
}


