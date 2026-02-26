using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Docket.Tests.Workflows;
using Docket.Api.Services;
using System.Net.Http.Json;

namespace Docket.Tests.Workflows;

/// <summary>
/// Tests for carry-forward behavior when creating new Minutes after finalized Minutes.
///
/// Carry-forward contract:
///   - Only Recurring topics that are IsOpen carry forward
///   - Only Open action items on those topics carry forward
///   - The original action item is marked Deferred (system-set) — audit trail preserved
///   - A new action item is created pointing back via SourceActionItemId
///   - Labels carry forward; AssignedInAbsentia resets to false
///   - Completed and Abandoned items do NOT carry forward
///   - Deferred items (human-set) DO carry forward — they represent unresolved liability
///
/// Open design question (see ADR): what happens to Open items on non-Recurring topics
/// at finalization? Current behavior: they carry forward only if on Recurring topics.
/// Non-recurring open items at finalization are a gap — no gate, no auto-abandon.
/// This should be resolved in an ADR before production use.
/// </summary>
[Collection("WorkflowTests")]
public class CarryForwardWorkflowTests : WorkflowTestBase
{
    public CarryForwardWorkflowTests(WorkflowTestFactory factory)
        : base(factory) { }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateSeriesAsync(string name)
    {
        var body = await PostAndDeserialize<JsonElement>("/series", new
        {
            name,
            project = "Carry-Forward Tests"
        });
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateMinutesAsync(Guid seriesId)
    {
        var body = await PostAndDeserialize<JsonElement>("/minutes", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateRecurringTopicAsync(Guid minutesId, string title)
    {
        var body = await PostAndDeserialize<JsonElement>(
            $"/minutes/{minutesId}/topics", new
            {
                title,
                type = "Recurring"
            });
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateDiscussionTopicAsync(Guid minutesId, string title)
    {
        var body = await PostAndDeserialize<JsonElement>(
            $"/minutes/{minutesId}/topics", new
            {
                title,
                type = "Discussion"
            });
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateActionItemAsync(Guid topicId, string title)
    {
        var body = await PostAndDeserialize<JsonElement>(
            $"/topics/{topicId}/action-items", new
            {
                title,
                responsibleId = StubCurrentUserService.StubUserId,
                priority = 2
            });
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task FinalizeAsync(Guid minutesId)
    {
        var response = await Client.PostAsJsonAsync(
            $"/minutes/{minutesId}/finalize", new { });
        response.IsSuccessStatusCode.Should().BeTrue(
            $"finalize {minutesId} must succeed for test setup");
    }

    private async Task<JsonElement[]> GetTopicsAsync(Guid minutesId)
    {
        return await GetAndDeserialize<JsonElement[]>(
            $"/minutes/{minutesId}/topics");
    }

    private async Task<JsonElement[]> GetActionItemsForTopicAsync(Guid topicId)
    {
        var topic = await GetAndDeserialize<JsonElement>($"/topics/{topicId}");
        return topic.GetProperty("actionItems").EnumerateArray().ToArray();
    }

    private async Task<JsonElement> GetActionItemAsync(Guid actionItemId)
    {
        return await GetAndDeserialize<JsonElement>(
            $"/action-items/{actionItemId}");
    }

    private async Task<JsonElement[]> GetHistoryAsync(Guid actionItemId)
    {
        return await GetAndDeserialize<JsonElement[]>(
            $"/action-items/{actionItemId}/history");
    }
    // ── Core carry-forward behavior ───────────────────────────────

    /// <summary>
    /// The fundamental carry-forward: a recurring topic with an open action item
    /// produces a new topic and new action item in the next minutes.
    /// </summary>
    [Fact]
    public async Task CarryForward_RecurringTopicWithOpenItem_CreatesNewTopicAndItem()
    {
        var seriesId = await CreateSeriesAsync("CF-Basic");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        await FinalizeAsync(m1Id);

        // Create second minutes — carry-forward should fire
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2Topics = await GetTopicsAsync(m2Id);
        m2Topics.Should().HaveCount(1,
            "one recurring open topic should have carried forward");

        var carriedTopic = m2Topics[0];
        carriedTopic.GetProperty("title").GetString()
            .Should().Be("Bridge Assignment");
        carriedTopic.GetProperty("type").GetString()
            .Should().Be("Recurring");

        var carriedTopicId = Guid.Parse(
            carriedTopic.GetProperty("id").GetString()!);
        var items = await GetActionItemsForTopicAsync(carriedTopicId);
        items.Should().HaveCount(1,
            "one open action item should have carried forward");

        var carriedItem = items[0];
        carriedItem.GetProperty("title").GetString()
            .Should().Be("Decide who builds the bridge");
        carriedItem.GetProperty("status").GetString()
            .Should().Be("Open",
            "carried-forward item starts as Open in the new minutes");
        carriedItem.GetProperty("sourceActionItemId").GetString()
            .Should().Be(a1Id.ToString(),
            "carried item must point back to its source");
    }

    /// <summary>
    /// The original item is marked Deferred when carried forward.
    /// This is a system-set state — not a human decision.
    /// </summary>
    [Fact]
    public async Task CarryForward_OriginalItem_IsMarkedDeferred()
    {
        var seriesId = await CreateSeriesAsync("CF-Deferred");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        await FinalizeAsync(m1Id);

        await CreateMinutesAsync(seriesId); // triggers carry-forward

        var original = await GetActionItemAsync(a1Id);
        original.GetProperty("status").GetString()
            .Should().Be("Deferred",
            "carry-forward must mark the original item as Deferred");
    }

    /// <summary>
    /// Completed items do not carry forward — they are resolved, terminal.
    /// </summary>
    [Fact]
    public async Task CarryForward_CompletedItem_DoesNotCarryForward()
    {
        var seriesId = await CreateSeriesAsync("CF-Completed");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");

        // Mark completed before finalizing
        var patchResponse = await Patch($"/action-items/{a1Id}",
            new { status = "done" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await FinalizeAsync(m1Id);
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2Topics = await GetTopicsAsync(m2Id);
        // Topic may or may not carry forward depending on IsOpen state,
        // but if it does, it should have no action items
        if (m2Topics.Length > 0)
        {
            var carriedTopicId = Guid.Parse(
                m2Topics[0].GetProperty("id").GetString()!);
            var items = await GetActionItemsForTopicAsync(carriedTopicId);
            items.Should().BeEmpty(
                "done items must not carry forward");
        }
        // If no topics carried forward at all, that also satisfies the contract
    }

    /// <summary>
    /// Abandoned items do not carry forward.
    /// </summary>
    [Fact]
    public async Task CarryForward_AbandonedItem_DoesNotCarryForward()
    {
        var seriesId = await CreateSeriesAsync("CF-Abandoned");

        // Meeting 1 — create item then abandon the whole minutes
        // This is the only way to get an item into Abandoned state (system-set)
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        var abandonResponse = await Post($"/minutes/{m1Id}/abandon",
            new { note = "Meeting cancelled — item never discussed" });
        abandonResponse.IsSuccessStatusCode.Should().BeTrue();

        // Now create a fresh second minutes for this series
        // (abandon cleared the draft gate)
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2Topics = await GetTopicsAsync(m2Id);
        m2Topics.Should().BeEmpty(
            "abandoned items must not carry forward — abandonment is terminal");
    }

    /// <summary>
    /// Human-set Deferred items DO carry forward.
    /// Deferred = "must be resolved, but not by this meeting."
    /// The liability persists until explicitly completed or abandoned.
    /// </summary>
    [Fact]
    public async Task CarryForward_HumanDeferredItem_DoesCarryForward()
    {
        var seriesId = await CreateSeriesAsync("CF-HumanDeferred");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");

        // NOTE: Current carry-forward code only picks up Open items.
        // If human-set Deferred items should also carry forward, the
        // Where clause in CreateMinutes needs to include Deferred.
        // This test documents the INTENDED behavior — if it fails,
        // the implementation needs updating, not the test.
        var patchResponse = await Patch($"/action-items/{a1Id}",
            new { status = "Deferred" });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await FinalizeAsync(m1Id);
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2Topics = await GetTopicsAsync(m2Id);
        m2Topics.Should().HaveCount(1,
            "recurring topic with deferred item should carry forward");

        var carriedTopicId = Guid.Parse(
            m2Topics[0].GetProperty("id").GetString()!);
        var items = await GetActionItemsForTopicAsync(carriedTopicId);
        items.Should().HaveCount(1,
            "human-deferred items represent unresolved liability and must carry forward");
    }

    /// <summary>
    /// Non-recurring topics do not carry forward regardless of item status.
    /// </summary>
    [Fact]
    public async Task CarryForward_NonRecurringTopic_DoesNotCarryForward()
    {
        var seriesId = await CreateSeriesAsync("CF-NonRecurring");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateDiscussionTopicAsync(m1Id, "One-time discussion");
        var a1Id = await CreateActionItemAsync(t1Id, "Do the thing");

        // Non-recurring open items must be resolved before finalization (ADR-006)
        var doneResponse = await Patch($"/action-items/{a1Id}",
            new { status = "done" });
        doneResponse.IsSuccessStatusCode.Should().BeTrue();

        await FinalizeAsync(m1Id);

        var m2Id = await CreateMinutesAsync(seriesId);
        var m2Topics = await GetTopicsAsync(m2Id);
        m2Topics.Should().BeEmpty(
            "non-recurring topics must not carry forward");
    }

    /// <summary>
    /// Labels on action items carry forward to the new item.
    /// </summary>
    [Fact]
    public async Task CarryForward_Labels_ArePreservedOnCarriedItem()
    {
        var seriesId = await CreateSeriesAsync("CF-Labels");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");

        // Get a system label and apply it
        var labels = await GetAndDeserialize<JsonElement[]>("/labels");
        var labelId = labels![0].GetProperty("id").GetString()!;
        var labelResponse = await Post(
            $"/action-items/{a1Id}/labels", new { labelId });
        labelResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        await FinalizeAsync(m1Id);
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2Topics = await GetTopicsAsync(m2Id);
        var carriedTopicId = Guid.Parse(
            m2Topics[0].GetProperty("id").GetString()!);
        var items = await GetActionItemsForTopicAsync(carriedTopicId);
        var carriedItem = items[0];

        var carriedLabels = carriedItem.GetProperty("labels")
            .EnumerateArray().ToArray();
        carriedLabels.Should().HaveCount(1,
            "labels must carry forward to the new action item");
        carriedLabels[0].GetProperty("id").GetString()
            .Should().Be(labelId);
    }

    // ── Lineage chain ─────────────────────────────────────────────

    /// <summary>
    /// After one carry-forward, the history of the new item is [original, copy].
    /// </summary>
    [Fact]
    public async Task CarryForward_History_ShowsTwoItemChain()
    {
        var seriesId = await CreateSeriesAsync("CF-History-2");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        await FinalizeAsync(m1Id);

        var m2Id = await CreateMinutesAsync(seriesId);
        var m2Topics = await GetTopicsAsync(m2Id);
        var carriedTopicId = Guid.Parse(
            m2Topics[0].GetProperty("id").GetString()!);
        var items = await GetActionItemsForTopicAsync(carriedTopicId);
        var a2Id = Guid.Parse(items[0].GetProperty("id").GetString()!);

        var history = await GetHistoryAsync(a2Id);
        history.Should().HaveCount(2,
            "history of carried item should be [original, copy]");
        history[0].GetProperty("id").GetString()
            .Should().Be(a1Id.ToString(), "first entry is the original");
        history[1].GetProperty("id").GetString()
            .Should().Be(a2Id.ToString(), "second entry is the carried copy");
    }

    /// <summary>
    /// After two carry-forwards, the history chain has three entries.
    /// This verifies the chain is linked list, not just parent→child.
    /// </summary>
    [Fact]
    public async Task CarryForward_History_ShowsThreeItemChainAfterTwoCarryForwards()
    {
        var seriesId = await CreateSeriesAsync("CF-History-3");

        // Meeting 1
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        await FinalizeAsync(m1Id);

        // Meeting 2 — first carry-forward
        var m2Id = await CreateMinutesAsync(seriesId);
        var m2Topics = await GetTopicsAsync(m2Id);
        var t2Id = Guid.Parse(m2Topics[0].GetProperty("id").GetString()!);
        var m2Items = await GetActionItemsForTopicAsync(t2Id);
        var a2Id = Guid.Parse(m2Items[0].GetProperty("id").GetString()!);
        await FinalizeAsync(m2Id);

        // Meeting 3 — second carry-forward
        var m3Id = await CreateMinutesAsync(seriesId);
        var m3Topics = await GetTopicsAsync(m3Id);
        var t3Id = Guid.Parse(m3Topics[0].GetProperty("id").GetString()!);
        var m3Items = await GetActionItemsForTopicAsync(t3Id);
        var a3Id = Guid.Parse(m3Items[0].GetProperty("id").GetString()!);

        var history = await GetHistoryAsync(a3Id);
        history.Should().HaveCount(3,
            "history should be [m1 item, m2 item, m3 item]");
        history[0].GetProperty("id").GetString()
            .Should().Be(a1Id.ToString(), "root is the original");
        history[1].GetProperty("id").GetString()
            .Should().Be(a2Id.ToString(), "middle is first carry-forward");
        history[2].GetProperty("id").GetString()
            .Should().Be(a3Id.ToString(), "tail is second carry-forward");
    }

    /// <summary>
    /// Querying history from any point in the chain returns the full chain.
    /// The original item's history also shows its descendants.
    /// </summary>
    [Fact]
    public async Task CarryForward_History_IsAccessibleFromAnyPointInChain()
    {
        var seriesId = await CreateSeriesAsync("CF-History-Any");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Bridge Assignment");
        var a1Id = await CreateActionItemAsync(t1Id, "Decide who builds the bridge");
        await FinalizeAsync(m1Id);

        var m2Id = await CreateMinutesAsync(seriesId);
        var m2Topics = await GetTopicsAsync(m2Id);
        var t2Id = Guid.Parse(m2Topics[0].GetProperty("id").GetString()!);
        var m2Items = await GetActionItemsForTopicAsync(t2Id);
        var a2Id = Guid.Parse(m2Items[0].GetProperty("id").GetString()!);

        // History from the original should show both items
        var historyFromRoot = await GetHistoryAsync(a1Id);
        historyFromRoot.Should().HaveCount(2,
            "history from original must include its descendants");

        // History from the copy should also show both items
        var historyFromCopy = await GetHistoryAsync(a2Id);
        historyFromCopy.Should().HaveCount(2,
            "history from copy must include the full chain");
    }

    // ── PreviousMinutesId linkage ─────────────────────────────────

    /// <summary>
    /// New minutes carry a PreviousMinutesId pointing to the most recent
    /// minutes regardless of status — this is the transcript chain, not
    /// just the finalized chain.
    /// </summary>
    [Fact]
    public async Task CreateMinutes_SetsCorrectPreviousMinutesId()
    {
        var seriesId = await CreateSeriesAsync("CF-PrevId");
        var m1Id = await CreateMinutesAsync(seriesId);
        await FinalizeAsync(m1Id);

        var m2Id = await CreateMinutesAsync(seriesId);
        var m2 = await GetAndDeserialize<JsonElement>($"/minutes/{m2Id}");

        m2.GetProperty("previousMinutesId").GetString()
            .Should().Be(m1Id.ToString(),
            "new minutes must link back to the previous minutes");
    }

    // ── Pinned global note ────────────────────────────────────────

    /// <summary>
    /// A pinned global note carries forward to new minutes.
    /// </summary>
    [Fact]
    public async Task CarryForward_PinnedGlobalNote_CarriesForward()
    {
        var seriesId = await CreateSeriesAsync("CF-PinnedNote");
        var m1Id = await CreateMinutesAsync(seriesId);

        // Set and pin the global note
        var patchResponse = await Patch($"/minutes/{m1Id}", new
        {
            globalNote = "Always read before the meeting",
            globalNotePinned = true
        });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await FinalizeAsync(m1Id);
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2 = await GetAndDeserialize<JsonElement>($"/minutes/{m2Id}");
        m2.GetProperty("globalNote").GetString()
            .Should().Be("Always read before the meeting",
            "pinned global note must carry forward");
    }

    /// <summary>
    /// An unpinned global note does not carry forward.
    /// </summary>
    [Fact]
    public async Task CarryForward_UnpinnedGlobalNote_DoesNotCarryForward()
    {
        var seriesId = await CreateSeriesAsync("CF-UnpinnedNote");
        var m1Id = await CreateMinutesAsync(seriesId);

        var patchResponse = await Patch($"/minutes/{m1Id}", new
        {
            globalNote = "Meeting-specific note",
            globalNotePinned = false
        });
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await FinalizeAsync(m1Id);
        var m2Id = await CreateMinutesAsync(seriesId);

        var m2 = await GetAndDeserialize<JsonElement>($"/minutes/{m2Id}");
        var note = m2.GetProperty("globalNote");
        var noteValue = note.ValueKind == JsonValueKind.Null
            ? null
            : note.GetString();
        noteValue.Should().BeNullOrEmpty(
            "unpinned global note must not carry forward");
    }

    [Fact]
    public async Task FinalizeMinutes_WithOpenNonRecurringItem_Returns409()
    {
        var seriesId = await CreateSeriesAsync("CF-FinalizeGate");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateDiscussionTopicAsync(m1Id, "One-time discussion");
        await CreateActionItemAsync(t1Id, "Do the thing");

        var response = await Post($"/minutes/{m1Id}/finalize", new { });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "finalization must be blocked by open items on non-recurring topics");
    }

    [Fact]
    public async Task FinalizeMinutes_WithOpenRecurringItem_Succeeds()
    {
        var seriesId = await CreateSeriesAsync("CF-FinalizeRecurring");
        var m1Id = await CreateMinutesAsync(seriesId);
        var t1Id = await CreateRecurringTopicAsync(m1Id, "Standing agenda item");
        await CreateActionItemAsync(t1Id, "Ongoing task");

        var response = await Post($"/minutes/{m1Id}/finalize", new { });
        response.IsSuccessStatusCode.Should().BeTrue(
            "open items on recurring topics must not block finalization — they carry forward");
    }
}
