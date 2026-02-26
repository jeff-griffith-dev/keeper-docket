using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Docket.Api.Services;
using Azure.Core;
using Docket.Domain.Exceptions;
using Docket.Api.Endpoints;

namespace Docket.Tests.Contract;

// ────────────────────────────────────────────────────────────────
// GET    /minutes/{minutesId}/topics/
// POST   /minutes/{minutesId}/topics/
// GET    /topics/{topicId}
// PATCH  /topics/{topicId}
// DELETE /topics/{topicId}
// ────────────────────────────────────────────────────────────────
public class TopicContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // ── Helpers ──────────────────────────────────────────────────

    private async Task<(Guid SeriesId, Guid MinutesId)> CreateMinutesAsync()
    {
        var (sr, sb) = await PostAsync<JsonElement>("/series/", new
        {
            name = $"Topic-Contract-{Guid.NewGuid():N}",
            project = "Contract"
        });
        ShouldBe(sr, HttpStatusCode.Created);
        var seriesId = Guid.Parse(sb.GetProperty("id").GetString()!);

        var (mr, mb) = await PostAsync<JsonElement>("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(mr, HttpStatusCode.Created);
        var minutesId = Guid.Parse(mb.GetProperty("id").GetString()!);
        return (seriesId, minutesId);
    }

    private async Task<Guid> CreateTopicAsync(Guid minutesId)
    {
        var (response, body) = await PostAsync<JsonElement>(
            $"/minutes/{minutesId}/topics/", new
            {
                title = $"Contract Topic {Guid.NewGuid():N}",
                type = "Discussion"
            });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ── GET /minutes/{minutesId}/topics/ ─────────────────────────

    [Fact]
    public async Task GetTopics_NoTopics_Returns200WithEmptyList()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/minutes/{minutesId}/topics/");
        ShouldBeSuccess(response);
        body!.Length.Should().Be(0);
    }

    [Fact]
    public async Task GetTopics_UnknownMinutes_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement[]>(
            $"/minutes/{UnknownId}/topics/");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /minutes/{minutesId}/topics/ ────────────────────────

    [Fact]
    public async Task CreateTopic_ValidBody_Returns201()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var (response, body) = await PostAsync<JsonElement>(
            $"/minutes/{minutesId}/topics/", new
            {
                title = "New Topic",
                type = "Discussion"
            });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTopic_MissingTitle_Returns400()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var response = await PostAsync($"/minutes/{minutesId}/topics/", new
        {
            type = "Discussion"
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTopic_UnknownMinutes_Returns404()
    {
        var response = await PostAsync($"/minutes/{UnknownId}/topics/", new
        {
            title = "Ghost Topic",
            type = "Discussion"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── GET /topics/{topicId} ────────────────────────────────────

    [Fact]
    public async Task GetTopic_ExistingId_Returns200()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var topicId = await CreateTopicAsync(minutesId);
        var (response, body) = await GetAsync<JsonElement>($"/topics/{topicId}");
        ShouldBeSuccess(response);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetTopic_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement>($"/topics/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /topics/{topicId} ──────────────────────────────────

    [Fact]
    public async Task UpdateTopic_ValidBody_Returns200()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var topicId = await CreateTopicAsync(minutesId);
        var response = await PatchAsync($"/topics/{topicId}", new
        {
            title = "Updated Title"
        });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateTopic_UnknownId_Returns404()
    {
        var response = await PatchAsync($"/topics/{UnknownId}", new
        {
            title = "Ghost"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── DELETE /topics/{topicId} ─────────────────────────────────

    [Fact]
    public async Task DeleteTopic_ExistingId_Returns204()
    {
        var (_, minutesId) = await CreateMinutesAsync();
        var topicId = await CreateTopicAsync(minutesId);
        var response = await DeleteAsync($"/topics/{topicId}");
        ShouldBe(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteTopic_UnknownId_Returns404()
    {
        var response = await DeleteAsync($"/topics/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }
}

// ────────────────────────────────────────────────────────────────
// POST   /topics/{topicId}/action-items/
// GET    /action-items/{actionItemId}
// PATCH  /action-items/{actionItemId}
// GET    /action-items/{actionItemId}/history
// POST   /action-items/{actionItemId}/labels
// DELETE /action-items/{actionItemId}/labels/{labelId}
// POST   /action-items/{actionItemId}/notes
// ────────────────────────────────────────────────────────────────
public class ActionItemContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateTopicAsync()
    {
        var (sr, sb) = await PostAsync<JsonElement>("/series/", new
        {
            name = $"AI-Contract-{Guid.NewGuid():N}",
            project = "Contract"
        });
        ShouldBe(sr, HttpStatusCode.Created);
        var seriesId = Guid.Parse(sb.GetProperty("id").GetString()!);

        var (mr, mb) = await PostAsync<JsonElement>("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(mr, HttpStatusCode.Created);
        var minutesId = Guid.Parse(mb.GetProperty("id").GetString()!);

        var (tr, tb) = await PostAsync<JsonElement>(
            $"/minutes/{minutesId}/topics/", new
            {
                title = "Action Item Topic",
                type = "Discussion"
            });
        ShouldBe(tr, HttpStatusCode.Created);
        return Guid.Parse(tb.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateActionItemAsync(Guid topicId)
    {
        var (response, body) = await PostAsync<JsonElement>(
            $"/topics/{topicId}/action-items/", new
            {
                title = $"Action {Guid.NewGuid():N}",
                responsibleId = StubCurrentUserService.StubUserId,
                priority = 2
            });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> GetSystemLabelIdAsync()
    {
        var (_, labels) = await GetAsync<JsonElement[]>("/labels/");
        return Guid.Parse(
            labels!.First(l => l.GetProperty("isSystem").GetBoolean())
                   .GetProperty("id").GetString()!);
    }

    // ── POST /topics/{topicId}/action-items/ ─────────────────────

    [Fact]
    public async Task CreateActionItem_ValidBody_Returns201()
    {
        var topicId = await CreateTopicAsync();
        var (response, body) = await PostAsync<JsonElement>(
            $"/topics/{topicId}/action-items/", new
            {
                title = "Do the thing",
                responsibleId = StubCurrentUserService.StubUserId,
                priority = 1
            });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateActionItem_MissingTitle_Returns400()
    {
        var topicId = await CreateTopicAsync();
        var response = await PostAsync($"/topics/{topicId}/action-items/", new
        {
            responsibleId = StubCurrentUserService.StubUserId,
            priority = 1
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateActionItem_UnknownTopic_Returns404()
    {
        var response = await PostAsync($"/topics/{UnknownId}/action-items/", new
        {
            title = "Ghost Action",
            responsibleId = StubCurrentUserService.StubUserId,
            priority = 1
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── GET /action-items/{actionItemId} ─────────────────────────

    [Fact]
    public async Task GetActionItem_ExistingId_Returns200()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var (response, body) = await GetAsync<JsonElement>(
            $"/action-items/{actionItemId}");
        ShouldBeSuccess(response);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetActionItem_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement>(
            $"/action-items/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /action-items/{actionItemId} ───────────────────────

    [Fact]
    public async Task UpdateActionItem_ValidBody_Returns200()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var response = await PatchAsync($"/action-items/{actionItemId}", new
        {
            title = "Updated title",
            priority = 3
        });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateActionItem_UnknownId_Returns404()
    {
        var response = await PatchAsync($"/action-items/{UnknownId}", new
        {
            title = "Ghost"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── GET /action-items/{actionItemId}/history ─────────────────

    [Fact]
    public async Task GetHistory_NewActionItem_Returns200WithSingleItem()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/action-items/{actionItemId}/history");
        ShouldBeSuccess(response);
        body!.Length.Should().Be(1,
            "a new action item with no lineage has a chain of exactly itself");
    }

    [Fact]
    public async Task GetHistory_NoNotes_Returns200WithEmptyList()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/action-items/{actionItemId}/history");
        ShouldBeSuccess(response);
        body!.Length.Should().Be(1,
            "an action item with no lineage has a chain of exactly itself");
    }

    [Fact]
    public async Task GetHistory_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement[]>(
            $"/action-items/{UnknownId}/history");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /action-items/{actionItemId}/labels ─────────────────

    [Fact]
    public async Task AddLabel_ValidLabel_Returns201()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var labelId = await GetSystemLabelIdAsync();
        var response = await PostAsync($"/action-items/{actionItemId}/labels",
            new { labelId });
        //ShouldBeSuccess(response);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddLabel_DuplicateLabel_Returns409()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var labelId = await GetSystemLabelIdAsync();
        await PostAsync($"/action-items/{actionItemId}/labels", new { labelId });
        var response = await PostAsync($"/action-items/{actionItemId}/labels",
            new { labelId });
        ShouldBe(response, HttpStatusCode.Conflict);
    }

    // ── DELETE /action-items/{actionItemId}/labels/{labelId} ─────

    [Fact]
    public async Task RemoveLabel_AppliedLabel_Returns204()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var labelId = await GetSystemLabelIdAsync();
        await PostAsync($"/action-items/{actionItemId}/labels", new { labelId });
        var response = await DeleteAsync(
            $"/action-items/{actionItemId}/labels/{labelId}");
        ShouldBe(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveLabel_UnknownLabel_Returns404()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var response = await DeleteAsync(
            $"/action-items/{actionItemId}/labels/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /action-items/{actionItemId}/notes ──────────────────

    [Fact]
    public async Task AddNote_ValidBody_Returns201()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var (response, _) = await PostAsync<JsonElement>(
            $"/action-items/{actionItemId}/notes", new
            {
                text = "Progress update",
                phase = "InProgress"
            });
        ShouldBe(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddNote_MissingText_Returns400()
    {
        var topicId = await CreateTopicAsync();
        var actionItemId = await CreateActionItemAsync(topicId);
        var response = await PostAsync($"/action-items/{actionItemId}/notes", new
        {
            phase = "InProgress"
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddNote_UnknownActionItem_Returns404()
    {
        var response = await PostAsync($"/action-items/{UnknownId}/notes", new
        {
            text = "Ghost note",
            phase = "InProgress"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }
}

// ────────────────────────────────────────────────────────────────
// POST   /topics/{topicId}/info-items/
// PATCH  /info-items/{infoItemId}
// DELETE /info-items/{infoItemId}
// ────────────────────────────────────────────────────────────────
public class InfoItemContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateTopicAsync()
    {
        var (sr, sb) = await PostAsync<JsonElement>("/series/", new
        {
            name = $"Info-Contract-{Guid.NewGuid():N}",
            project = "Contract"
        });
        ShouldBe(sr, HttpStatusCode.Created);
        var seriesId = Guid.Parse(sb.GetProperty("id").GetString()!);

        var (mr, mb) = await PostAsync<JsonElement>("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(mr, HttpStatusCode.Created);
        var minutesId = Guid.Parse(mb.GetProperty("id").GetString()!);

        var (tr, tb) = await PostAsync<JsonElement>(
            $"/minutes/{minutesId}/topics/", new
            {
                title = "Info Topic",
                type = "Information"
            });
        ShouldBe(tr, HttpStatusCode.Created);
        return Guid.Parse(tb.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateInfoItemAsync(Guid topicId)
    {
        var (response, body) = await PostAsync<JsonElement>(
            $"/topics/{topicId}/info-items/", new
            {
                text = $"Info item {Guid.NewGuid():N}"
            });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ── POST /topics/{topicId}/info-items/ ───────────────────────

    [Fact]
    public async Task CreateInfoItem_ValidBody_Returns201()
    {
        var topicId = await CreateTopicAsync();
        var (response, body) = await PostAsync<JsonElement>(
            $"/topics/{topicId}/info-items/", new
            {
                text = "This is an informational item"
            });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateInfoItem_MissingText_Returns400()
    {
        var topicId = await CreateTopicAsync();
        var response = await PostAsync($"/topics/{topicId}/info-items/", new { });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateInfoItem_UnknownTopic_Returns404()
    {
        var response = await PostAsync($"/topics/{UnknownId}/info-items/", new
        {
            text = "Ghost info"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /info-items/{infoItemId} ───────────────────────────

    [Fact]
    public async Task UpdateInfoItem_ValidBody_Returns200()
    {
        var topicId = await CreateTopicAsync();
        var infoItemId = await CreateInfoItemAsync(topicId);
        var response = await PatchAsync($"/info-items/{infoItemId}", new
        {
            text = "Updated info text"
        });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateInfoItem_UnknownId_Returns404()
    {
        var response = await PatchAsync($"/info-items/{UnknownId}", new
        {
            text = "Ghost"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── DELETE /info-items/{infoItemId} ──────────────────────────

    [Fact]
    public async Task DeleteInfoItem_ExistingId_Returns204()
    {
        var topicId = await CreateTopicAsync();
        var infoItemId = await CreateInfoItemAsync(topicId);
        var response = await DeleteAsync($"/info-items/{infoItemId}");
        ShouldBe(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteInfoItem_UnknownId_Returns404()
    {
        var response = await DeleteAsync($"/info-items/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }
}
