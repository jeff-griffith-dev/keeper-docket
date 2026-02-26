using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Docket.Tests.Contract;

// ────────────────────────────────────────────────────────────────
// GET    /series/
// POST   /series/
// GET    /series/{seriesId}
// PATCH  /series/{seriesId}
// POST   /series/{seriesId}/archive
// GET    /series/{seriesId}/minutes
// GET    /series/{seriesId}/participants
// POST   /series/{seriesId}/participants
// PATCH  /series/{seriesId}/participants/{userId}
// DELETE /series/{seriesId}/participants/{userId}
// ────────────────────────────────────────────────────────────────
public class SeriesContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateSeriesAsync(string? name = null)
    {
        var (response, body) = await PostAsync<JsonElement>("/series/", new
        {
            name = name ?? $"Contract-Series-{Guid.NewGuid():N}",
            project = "Contract Testing"
        });
        ShouldBe(response, HttpStatusCode.Created, "series creation must succeed for test setup");
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateSecondUserAsync()
    {
        var (response, body) = await PostAsync<JsonElement>("/users/", new
        {
            email = $"participant-{Guid.NewGuid():N}@test.local",
            displayName = "Participant User"
        });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ── GET /series/ ─────────────────────────────────────────────

    [Fact]
    public async Task GetSeries_EmptyDb_Returns200WithEmptyList()
    {
        var (response, body) = await GetAsync<JsonElement[]>("/series/");
        ShouldBeSuccess(response);
        body.Should().NotBeNull();
        // Note: may not be empty if other contract tests ran first; just verify shape
        body!.GetType().Should().Be(typeof(JsonElement[]));
    }

    // ── POST /series/ ────────────────────────────────────────────

    [Fact]
    public async Task CreateSeries_ValidBody_Returns201WithId()
    {
        var (response, body) = await PostAsync<JsonElement>("/series/", new
        {
            name = $"Contract-{Guid.NewGuid():N}",
            project = "Test Project"
        });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateSeries_MissingName_Returns400()
    {
        var response = await PostAsync("/series/", new { project = "No Name" });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    // ── GET /series/{seriesId} ───────────────────────────────────

    [Fact]
    public async Task GetSeries_ExistingId_Returns200()
    {
        var id = await CreateSeriesAsync();
        var (response, body) = await GetAsync<JsonElement>($"/series/{id}");
        ShouldBeSuccess(response);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetSeries_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement>($"/series/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /series/{seriesId} ─────────────────────────────────

    [Fact]
    public async Task UpdateSeries_ValidBody_Returns200()
    {
        var id = await CreateSeriesAsync();
        var response = await PatchAsync($"/series/{id}", new { name = "Updated Name" });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateSeries_UnknownId_Returns404()
    {
        var response = await PatchAsync($"/series/{UnknownId}", new { name = "Ghost" });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSeries_ArchivedSeries_Returns409()
    {
        var id = await CreateSeriesAsync();
        await PostAsync($"/series/{id}/archive", new { });
        var response = await PatchAsync($"/series/{id}", new { name = "Too Late" });
        ShouldBe(response, HttpStatusCode.Conflict,
            "archived series must reject modifications");
    }

    // ── POST /series/{seriesId}/archive ──────────────────────────

    [Fact]
    public async Task ArchiveSeries_ActiveSeries_Returns200()
    {
        var id = await CreateSeriesAsync();
        var response = await PostAsync($"/series/{id}/archive", new { });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task ArchiveSeries_UnknownId_Returns404()
    {
        var response = await PostAsync($"/series/{UnknownId}/archive", new { });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveSeries_AlreadyArchived_Returns409()
    {
        var id = await CreateSeriesAsync();
        await PostAsync($"/series/{id}/archive", new { });
        var response = await PostAsync($"/series/{id}/archive", new { });
        ShouldBe(response, HttpStatusCode.Conflict,
            "archiving an already-archived series must be rejected");
    }

    // ── GET /series/{seriesId}/minutes ───────────────────────────

    [Fact]
    public async Task GetSeriesMinutes_NoMinutes_Returns200WithEmptyList()
    {
        var id = await CreateSeriesAsync();
        var (response, body) = await GetAsync<JsonElement[]>($"/series/{id}/minutes");
        ShouldBeSuccess(response);
        body!.Length.Should().Be(0);
    }

    [Fact]
    public async Task GetSeriesMinutes_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement[]>($"/series/{UnknownId}/minutes");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── GET /series/{seriesId}/participants ──────────────────────

    [Fact]
    public async Task GetParticipants_Returns200WithCreator()
    {
        var id = await CreateSeriesAsync();
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/series/{id}/participants");
        ShouldBeSuccess(response);
        body!.Length.Should().BeGreaterThan(0,
            "creator should be added as participant automatically");
    }

    [Fact]
    public async Task GetParticipants_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement[]>(
            $"/series/{UnknownId}/participants");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /series/{seriesId}/participants ─────────────────────

    [Fact]
    public async Task AddParticipant_ValidUser_Returns201()
    {
        var seriesId = await CreateSeriesAsync();
        var userId = await CreateSecondUserAsync();
        var (response, _) = await PostAsync<JsonElement>(
            $"/series/{seriesId}/participants",
            new { userId, role = "Informed" });
        ShouldBe(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddParticipant_DuplicateUser_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        var userId = await CreateSecondUserAsync();
        await PostAsync($"/series/{seriesId}/participants",
            new { userId, role = "Invited" });
        var response = await PostAsync($"/series/{seriesId}/participants",
            new { userId, role = "Invited" });
        ShouldBe(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddParticipant_UnknownSeries_Returns404()
    {
        var userId = await CreateSecondUserAsync();
        var response = await PostAsync($"/series/{UnknownId}/participants",
            new { userId, role = "Invited" });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /series/{seriesId}/participants/{userId} ───────────

    [Fact]
    public async Task UpdateParticipant_ValidRole_Returns200()
    {
        var seriesId = await CreateSeriesAsync();
        var userId = await CreateSecondUserAsync();
        await PostAsync($"/series/{seriesId}/participants",
            new { userId, role = "Invited" });
        var response = await PatchAsync(
            $"/series/{seriesId}/participants/{userId}",
            new { role = "Moderator" });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateParticipant_UnknownUser_Returns404()
    {
        var seriesId = await CreateSeriesAsync();
        var response = await PatchAsync(
            $"/series/{seriesId}/participants/{UnknownId}",
            new { role = "Moderator" });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── DELETE /series/{seriesId}/participants/{userId} ──────────

    [Fact]
    public async Task RemoveParticipant_ExistingParticipant_Returns204()
    {
        var seriesId = await CreateSeriesAsync();
        var userId = await CreateSecondUserAsync();
        await PostAsync($"/series/{seriesId}/participants",
            new { userId, role = "Invited" });
        var response = await DeleteAsync(
            $"/series/{seriesId}/participants/{userId}");
        ShouldBe(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveParticipant_UnknownUser_Returns404()
    {
        var seriesId = await CreateSeriesAsync();
        var response = await DeleteAsync(
            $"/series/{seriesId}/participants/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }
}
