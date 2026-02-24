using Docket.Api.Services;
using Docket.Domain.Entities;
using Docket.Domain.Exceptions;
using Docket.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Docket.Api.Endpoints;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? ExternalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static UserResponse From(User u) => new(
        u.Id, u.Email, u.DisplayName, u.ExternalId, u.CreatedAt, u.UpdatedAt);
}

public record CreateUserRequest(string Email, string DisplayName, string? ExternalId);
public record UpdateUserRequest(string? DisplayName, string? ExternalId);

public record OpenItemSummaryResponse(
    Guid Id,
    string Title,
    Guid ResponsibleId,
    DateOnly? DueDate,
    int Priority,
    string Status,
    bool AssignedInAbsentia,
    Guid SeriesId,
    string SeriesName,
    Guid MinutesId,
    string TopicTitle,
    DateTimeOffset CreatedAt);

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("users");

        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create a user");

        group.MapGet("/{userId:guid}", GetUser)
            .WithName("GetUser")
            .WithSummary("Get a user by ID");

        group.MapPatch("/{userId:guid}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Update display name or external ID");

        group.MapGet("/{userId:guid}/open-items", GetOpenItems)
            .WithName("GetUserOpenItems")
            .WithSummary("Get all open action items owned by a user across all series");
    }

    // POST /users
    private static async Task<IResult> CreateUser(
        CreateUserRequest request,
        DocketDbContext db,
        CancellationToken ct)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u => u.Email == emailNormalized, ct);
        if (exists)
            throw new EmailExistsException(emailNormalized);

        var user = new User
        {
            Email = emailNormalized,
            DisplayName = request.DisplayName.Trim(),
            ExternalId = request.ExternalId?.Trim()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/users/{user.Id}", UserResponse.From(user));
    }

    // GET /users/{userId}
    private static async Task<IResult> GetUser(
        Guid userId,
        DocketDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException(nameof(User), userId);

        return Results.Ok(UserResponse.From(user));
    }

    // PATCH /users/{userId}
    private static async Task<IResult> UpdateUser(
        Guid userId,
        UpdateUserRequest request,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (userId != currentUser.UserId)
            throw new ForbiddenException("You may only update your own user record.");

        var user = await db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException(nameof(User), userId);

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName.Trim();

        if (request.ExternalId is not null)
            user.ExternalId = request.ExternalId.Trim();

        await db.SaveChangesAsync(ct);
        return Results.Ok(UserResponse.From(user));
    }

    // GET /users/{userId}/open-items
    private static async Task<IResult> GetOpenItems(
        Guid userId,
        Guid? seriesId,
        string? project,
        DocketDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (userId != currentUser.UserId)
            throw new ForbiddenException("You may only view your own open items.");

        var query = db.ActionItems
            .Where(a => a.ResponsibleId == userId &&
                        a.Status == Domain.Enums.ActionItemStatus.Open)
            .Include(a => a.Topic)
                .ThenInclude(t => t!.Minutes)
                    .ThenInclude(m => m!.Series)
            .Include(a => a.ActionItemLabels)
                .ThenInclude(al => al.Label)
            .AsQueryable();

        if (seriesId.HasValue)
            query = query.Where(a => a.Topic!.Minutes!.SeriesId == seriesId.Value);

        if (!string.IsNullOrWhiteSpace(project))
            query = query.Where(a => a.Topic!.Minutes!.Series!.Project == project);

        var items = await query
            .OrderBy(a => a.DueDate == null)   // nulls last
            .ThenBy(a => a.DueDate)
            .ThenBy(a => a.Priority)
            .ToListAsync(ct);

        var response = items.Select(a => new OpenItemSummaryResponse(
            a.Id,
            a.Title,
            a.ResponsibleId,
            a.DueDate,
            a.Priority,
            a.Status.ToString(),
            a.AssignedInAbsentia,
            a.Topic!.Minutes!.SeriesId,
            a.Topic.Minutes.Series!.Name,
            a.Topic.MinutesId,
            a.Topic.Title,
            a.CreatedAt));

        return Results.Ok(response);
    }
}
