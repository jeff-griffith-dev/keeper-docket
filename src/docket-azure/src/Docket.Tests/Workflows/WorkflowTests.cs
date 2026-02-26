using Docket.Api.Services;
using Docket.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Docket.Tests.Workflows;

/// <summary>
/// Workflow tests that simulate real user journeys across multiple API calls.
/// Each test is a scenario, not an isolated operation.
///
/// These tests answer: "Does the system behave correctly when a user moves
/// through the full lifecycle?" — something the unit and endpoint tests
/// cannot verify on their own.
/// </summary>

// ---------------------------------------------------------------------------
// Shared infrastructure
// ---------------------------------------------------------------------------

/// <summary>
/// Isolated factory for workflow tests — fresh database per test class.
/// </summary>
public class WorkflowTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAliveConnection;

    public WorkflowTestFactory()
    {
        var dbName = $"docket-workflow-{Guid.NewGuid()}";
        _keepAliveConnection = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open(); // holds the in-memory DB alive for the factory's lifetime
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<DocketDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            var connStr = _keepAliveConnection.ConnectionString;
            services.AddDbContext<DocketDbContext>(options =>
                options.UseSqlite(connStr));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DocketDbContext>();
            db.Database.EnsureCreated();
            if (!db.Users.Any(u => u.Id == StubCurrentUserService.StubUserId))
            {
                db.Users.Add(new Docket.Domain.Entities.User
                {
                    Id = StubCurrentUserService.StubUserId,
                    Email = "dev@docket.local",
                    DisplayName = "Dev User (Stub)"
                });
                db.SaveChanges();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}

/// <summary>
/// Base class with shared HTTP helpers for workflow tests.
/// </summary>
public abstract class WorkflowTestBase : IClassFixture<WorkflowTestFactory>
{
    protected readonly HttpClient Client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    protected WorkflowTestBase(WorkflowTestFactory factory)
    {
        Client = factory.CreateClient();
    }

    protected async Task<T> PostAndDeserialize<T>(string url, object body)
    {
        var response = await Client.PostAsJsonAsync(url, body);
        response.IsSuccessStatusCode.Should().BeTrue(
            $"POST {url} failed with {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    protected async Task<T> GetAndDeserialize<T>(string url)
    {
        var response = await Client.GetAsync(url);
        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET {url} failed with {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    protected async Task<HttpResponseMessage> Post(string url, object body) =>
        await Client.PostAsJsonAsync(url, body);

    protected async Task<HttpResponseMessage> Patch(string url, object body) =>
        await Client.PatchAsJsonAsync(url, body);

    // Lightweight response records — just the fields the workflow tests care about
    protected record SeriesDto(Guid Id, string Name, string Status);
    protected record MinutesDto(Guid Id, Guid SeriesId, Guid? PreviousMinutesId, string Status, int Version);
    protected record TopicDto(Guid Id, Guid MinutesId, string Title, bool IsOpen, IEnumerable<ActionItemDto> ActionItems);
    protected record ActionItemDto(Guid Id, Guid? SourceActionItemId, string Title, string Status);
    protected record ProblemDto(string Title, string? Detail);
}

// ---------------------------------------------------------------------------
// Workflow 1: Full meeting cycle
// ---------------------------------------------------------------------------

/// <summary>
/// Simulates a moderator running two meetings end-to-end:
///   1. Create series
///   2. Create first Minutes
///   3. Add a recurring topic with an open action item
///   4. Finalize the first meeting
///   5. Create second Minutes — verify carry-forward created correctly
///   6. Verify the original action item is now Deferred
///   7. Verify the carried-forward copy is Open with SourceActionItemId set
/// </summary>
public class FullMeetingCycleWorkflowTests : WorkflowTestBase
{
    public FullMeetingCycleWorkflowTests(WorkflowTestFactory factory) : base(factory) { }

    [Fact]
    public async Task TwoMeetingCycle_CarryForwardCreatesCorrectLineage()
    {
        // --- Step 1: Create series ---
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Weekly Standup", project = "ProjectX" });
        series.Status.Should().Be("Active");

        // --- Step 2: Create first Minutes ---
        var meeting1 = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-7) });
        meeting1.Status.Should().Be("Draft");
        meeting1.PreviousMinutesId.Should().BeNull("this is the first meeting in the series");

        // --- Step 3: Add a recurring topic with an action item ---
        var topic = await PostAndDeserialize<TopicDto>(
            $"/minutes/{meeting1.Id}/topics",
            new { title = "API integration review", type = "Recurring" });

        var actionItem = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic.Id}/action-items",
            new
            {
                title = "Document the authentication flow",
                responsibleId = StubCurrentUserService.StubUserId,
                priority = 2
            });

        actionItem.Status.Should().Be("Open");

        // --- Step 4: Finalize the first meeting ---
        var finalized1 = await PostAndDeserialize<MinutesDto>(
            $"/minutes/{meeting1.Id}/finalize",
            new { notifyResponsibles = false, notifyAll = false });

        finalized1.Status.Should().Be("Finalized");
        finalized1.Version.Should().Be(1);

        // --- Step 5: Create second Minutes ---
        var meeting2 = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow });

        meeting2.Status.Should().Be("Draft");
        meeting2.PreviousMinutesId.Should().Be(meeting1.Id,
            "the chain must link back to the previous Minutes");

        // --- Step 6: Verify the original action item is now Deferred ---
        var originalItem = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{actionItem.Id}");

        originalItem.Status.Should().Be("Deferred",
            "carry-forward marks the original as Deferred so the chain reads correctly");

        // --- Step 7: Verify carried-forward copy exists and links back ---
        var meeting2Detail = await GetAndDeserialize<MinutesDetailDto>(
            $"/minutes/{meeting2.Id}");

        var carriedTopics = meeting2Detail.Topics.ToList();
        carriedTopics.Should().HaveCount(1,
            "the recurring open topic should have been carried forward");

        var carriedItems = carriedTopics[0].ActionItems.ToList();
        carriedItems.Should().HaveCount(1,
            "the open action item should have been carried forward");

        var carriedItem = carriedItems[0];
        carriedItem.Status.Should().Be("Open");
        carriedItem.SourceActionItemId.Should().Be(actionItem.Id,
            "the carried-forward copy must point back to its origin for lineage traversal");
        carriedItem.Title.Should().Be("Document the authentication flow");
    }

    // Extended DTO needed for the detail response
    private record MinutesDetailDto(
        Guid Id, string Status, int Version,
        IEnumerable<TopicDto> Topics);
}

// ---------------------------------------------------------------------------
// Workflow 2: Pre-creation gate
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies the pre-creation gate: a new Minutes cannot be created while
/// any prior draft exists in the series chain.
///
/// Journey:
///   1. Create series
///   2. Create first Minutes (draft)
///   3. Attempt to create second Minutes — expect 409
///   4. Finalize the first Minutes
///   5. Create second Minutes — succeeds
///   6. Abandon the second Minutes (with note)
///   7. Attempt to create third Minutes — expect 409 (abandoned blocks too? No — only drafts)
///   8. Create third Minutes — succeeds (abandoned is terminal, not a draft)
/// </summary>
public class PreCreationGateWorkflowTests : WorkflowTestBase
{
    public PreCreationGateWorkflowTests(WorkflowTestFactory factory) : base(factory) { }

    [Fact]
    public async Task CreateMinutes_WhileDraftExists_IsBlocked()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Gate Test Series" });

        // Create first Minutes — now a draft exists
        await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-7) });

        // Attempt second Minutes while first is still draft
        var blocked = await Post("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow });

        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "creating new Minutes while a draft exists must be blocked");

        var problem = await blocked.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.Should().Be("UNRESOLVED_DRAFTS_EXIST");
    }

    [Fact]
    public async Task CreateMinutes_AfterFinalizing_Succeeds()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Gate Resolve Series" });

        var draft = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-7) });

        // Finalize the draft
        await PostAndDeserialize<MinutesDto>(
            $"/minutes/{draft.Id}/finalize",
            new { notifyResponsibles = false, notifyAll = false });

        // Now creation should succeed
        var second = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow });

        second.Status.Should().Be("Draft");
        second.PreviousMinutesId.Should().Be(draft.Id);
    }

    [Fact]
    public async Task CreateMinutes_AfterAbandoning_Succeeds()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Gate Abandon Series" });

        var draft = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-7) });

        // Abandon the draft
        await PostAndDeserialize<MinutesDto>(
            $"/minutes/{draft.Id}/abandon",
            new { note = "Meeting was cancelled — no attendees showed up" });

        // Abandoned is terminal, not a draft — gate should not block
        var second = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow });

        second.Status.Should().Be("Draft");
    }
}

// ---------------------------------------------------------------------------
// Workflow 3: Abandonment cascade
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies that abandoning a Minutes atomically abandons all open and
/// deferred action items beneath it.
///
/// Journey:
///   1. Create series, Minutes, topic
///   2. Add three action items: one open, one deferred, one done
///   3. Abandon the Minutes with a note
///   4. Verify open item → Abandoned
///   5. Verify deferred item → Abandoned
///   6. Verify done item remains Done (terminal states are not affected)
///   7. Verify the Minutes itself shows AbandonmentNote
/// </summary>
public class AbandonmentCascadeWorkflowTests : WorkflowTestBase
{
    public AbandonmentCascadeWorkflowTests(WorkflowTestFactory factory) : base(factory) { }

    [Fact]
    public async Task AbandonMinutes_CascadesAbandonmentToOpenAndDeferredItems()
    {
        // Setup
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Cascade Test Series" });

        var minutes = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(1) });

        var topic = await PostAndDeserialize<TopicDto>(
            $"/minutes/{minutes.Id}/topics",
            new { title = "Work items" });

        // Create open item
        var openItem = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic.Id}/action-items",
            new { title = "Open task", responsibleId = StubCurrentUserService.StubUserId });

        // Create and defer an item
        var deferredItem = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic.Id}/action-items",
            new { title = "Deferred task", responsibleId = StubCurrentUserService.StubUserId });

        await Patch($"/action-items/{deferredItem.Id}", new { status = "Deferred" });

        // Create and complete an item
        var doneItem = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic.Id}/action-items",
            new { title = "Completed task", responsibleId = StubCurrentUserService.StubUserId });

        await Patch($"/action-items/{doneItem.Id}", new { status = "Done" });

        // --- Abandon the Minutes ---
        var abandonedMinutes = await PostAndDeserialize<AbandonedMinutesDto>(
            $"/minutes/{minutes.Id}/abandon",
            new { note = "Project was put on hold by client decision" });

        abandonedMinutes.Status.Should().Be("Abandoned");
        abandonedMinutes.AbandonmentNote.Should().Be("Project was put on hold by client decision");

        // --- Verify cascade ---
        var openItemAfter = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{openItem.Id}");
        openItemAfter.Status.Should().Be("Abandoned",
            "open items must be abandoned when their parent Minutes is abandoned");

        var deferredItemAfter = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{deferredItem.Id}");
        deferredItemAfter.Status.Should().Be("Abandoned",
            "deferred items must also be abandoned — a postponement that never resolved is still abandoned");

        var doneItemAfter = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{doneItem.Id}");
        doneItemAfter.Status.Should().Be("Done",
            "terminal Done items must not be affected by Minutes abandonment");
    }

    [Fact]
    public async Task AbandonMinutes_WithoutNote_IsRejected()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "No Note Series" });

        var minutes = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(1) });

        var response = await Post(
            $"/minutes/{minutes.Id}/abandon",
            new { note = "" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "abandonment without a note must be rejected");
    }

    private record AbandonedMinutesDto(
        Guid Id, string Status, string? AbandonmentNote);
}

// ---------------------------------------------------------------------------
// Workflow 4: Series archival cascade
// ---------------------------------------------------------------------------

/// <summary>
/// Verifies that archiving a series blocks if unresolved drafts exist,
/// and that archival atomically abandons all open items across all Minutes.
///
/// Journey:
///   1. Create series with two finalized Minutes, each with action items
///   2. Leave some items open, close some
///   3. Archive the series
///   4. Verify series status is Archived
///   5. Verify open items across both Minutes are now Abandoned
///   6. Verify done items remain Done
///   7. Verify archived series cannot be modified
/// </summary>
public class SeriesArchivalWorkflowTests : WorkflowTestBase
{
    public SeriesArchivalWorkflowTests(WorkflowTestFactory factory) : base(factory) { }

    [Fact]
    public async Task ArchiveSeries_AbandonesAllOpenItemsAcrossAllMinutes()
    {
        // --- Setup: two finalized meetings with items in various states ---
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Archival Test Series" });

        // First meeting
        var meeting1 = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-14) });

        var topic1 = await PostAndDeserialize<TopicDto>(
            $"/minutes/{meeting1.Id}/topics",
            new { title = "Meeting 1 topics" });

        var openItem1 = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic1.Id}/action-items",
            new { title = "Still open after meeting 1", responsibleId = StubCurrentUserService.StubUserId });

        var doneItem1 = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic1.Id}/action-items",
            new { title = "Completed in meeting 1", responsibleId = StubCurrentUserService.StubUserId });

        await Patch($"/action-items/{doneItem1.Id}", new { status = "Done" });

        // See ADR-006  for rationale on why we block finalize on open items for non-recurring meetings     
        await Patch($"/action-items/{openItem1.Id}", new { status = "Deferred" });

        await PostAndDeserialize<MinutesDto>(
            $"/minutes/{meeting1.Id}/finalize",
            new { notifyResponsibles = false, notifyAll = false });

        // Second meeting (carries forward the open item from meeting 1 automatically)
        var meeting2 = await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(-7) });

        var topic2 = await PostAndDeserialize<TopicDto>(
            $"/minutes/{meeting2.Id}/topics",
            new { title = "Meeting 2 new items" });

        var openItem2 = await PostAndDeserialize<ActionItemDto>(
            $"/topics/{topic2.Id}/action-items",
            new { title = "New item in meeting 2, never resolved", responsibleId = StubCurrentUserService.StubUserId });

        // See ADR-006  for rationale on why we block finalize on non-recurring meetings     
        await Patch($"/action-items/{openItem2.Id}", new { status = "Deferred" });

        await PostAndDeserialize<MinutesDto>(
            $"/minutes/{meeting2.Id}/finalize",
            new { notifyResponsibles = false, notifyAll = false });

        // --- Archive the series ---
        var archiveResponse = await Post($"/series/{series.Id}/archive", new { });
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var archivedSeries = await GetAndDeserialize<SeriesDto>($"/series/{series.Id}");
        archivedSeries.Status.Should().Be("Archived");

        // --- Deferred items will be Abandoned ---
        var openItem1After = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{openItem1.Id}");
        openItem1After.Status.Should().Be("Abandoned",
            "deferred items in finalized Minutes must be abandoned when the series is archived");

        var openItem2After = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{openItem2.Id}");
        openItem2After.Status.Should().Be("Abandoned");

        // --- Verify done items are untouched ---
        var doneItem1After = await GetAndDeserialize<ActionItemDto>(
            $"/action-items/{doneItem1.Id}");
        doneItem1After.Status.Should().Be("Done",
            "completed items must not be affected by series archival");
    }

    [Fact]
    public async Task ArchiveSeries_WithUnresolvedDraft_IsBlocked()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Draft Block Archive Series" });

        // Create a draft Minutes and leave it unresolved
        await PostAndDeserialize<MinutesDto>("/minutes",
            new { seriesId = series.Id, scheduledFor = DateTimeOffset.UtcNow.AddDays(1) });

        var archiveResponse = await Post($"/series/{series.Id}/archive", new { });

        archiveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "archiving a series with unresolved drafts must be blocked");

        var problem = await archiveResponse.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.Should().Be("UNRESOLVED_DRAFTS_EXIST");
    }

    [Fact]
    public async Task ArchivedSeries_CannotBeModified()
    {
        var series = await PostAndDeserialize<SeriesDto>("/series",
            new { name = "Immutable After Archive" });

        // Archive immediately (no minutes = no drafts to block)
        await Post($"/series/{series.Id}/archive", new { });

        // Attempt to update the archived series
        var updateResponse = await Patch($"/series/{series.Id}",
            new { name = "Trying to rename an archived series" });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "archived series must reject modifications");
    }
}
