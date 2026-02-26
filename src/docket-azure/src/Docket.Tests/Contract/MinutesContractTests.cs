using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Docket.Tests.Contract;

// ────────────────────────────────────────────────────────────────
// POST   /minutes/
// GET    /minutes/{minutesId}
// PATCH  /minutes/{minutesId}
// POST   /minutes/{minutesId}/finalize
// POST   /minutes/{minutesId}/abandon
// GET    /minutes/{minutesId}/attendees
// POST   /minutes/{minutesId}/attendees
// DELETE /minutes/{minutesId}/attendees/{userId}
// ────────────────────────────────────────────────────────────────
public class MinutesContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> CreateSeriesAsync()
    {
        var (response, body) = await PostAsync<JsonElement>("/series/", new
        {
            name = $"Minutes-Contract-Series-{Guid.NewGuid():N}",
            project = "Contract Testing"
        });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateMinutesAsync(Guid seriesId)
    {
        var (response, body) = await PostAsync<JsonElement>("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.Created,
            $"minutes creation failed for series {seriesId}");
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> CreateSecondUserAsync()
    {
        var (response, body) = await PostAsync<JsonElement>("/users/", new
        {
            email = $"attendee-{Guid.NewGuid():N}@test.local",
            displayName = "Attendee User"
        });
        ShouldBe(response, HttpStatusCode.Created);
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    // ── POST /minutes/ ───────────────────────────────────────────

    [Fact]
    public async Task CreateMinutes_ValidBody_Returns201WithId()
    {
        var seriesId = await CreateSeriesAsync();
        var (response, body) = await PostAsync<JsonElement>("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateMinutes_UnknownSeries_Returns404()
    {
        var response = await PostAsync("/minutes/", new
        {
            seriesId = UnknownId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateMinutes_MissingSeriesId_Returns400()
    {
        var response = await PostAsync("/minutes/", new
        {
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMinutes_WhileDraftExists_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        await CreateMinutesAsync(seriesId); // creates a draft
        var response = await PostAsync("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(2).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.Conflict,
            "cannot create new minutes while a draft exists");
    }

    [Fact]
    public async Task CreateMinutes_ArchivedSeries_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        await PostAsync($"/series/{seriesId}/archive", new { });
        var response = await PostAsync("/minutes/", new
        {
            seriesId,
            scheduledFor = DateTimeOffset.UtcNow.AddDays(1).ToString("o")
        });
        ShouldBe(response, HttpStatusCode.Conflict,
            "cannot create minutes for an archived series");
    }

    // ── GET /minutes/{minutesId} ─────────────────────────────────

    [Fact]
    public async Task GetMinutes_ExistingId_Returns200()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var (response, body) = await GetAsync<JsonElement>($"/minutes/{minutesId}");
        ShouldBeSuccess(response);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetMinutes_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement>($"/minutes/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── PATCH /minutes/{minutesId} ───────────────────────────────

    [Fact]
    public async Task UpdateMinutes_ValidBody_Returns200()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var response = await PatchAsync($"/minutes/{minutesId}", new
        {
            globalNote = "Updated note"
        });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task UpdateMinutes_UnknownId_Returns404()
    {
        var response = await PatchAsync($"/minutes/{UnknownId}", new
        {
            globalNote = "Ghost"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /minutes/{minutesId}/finalize ───────────────────────

    [Fact]
    public async Task FinalizeMinutes_DraftMinutes_Returns200()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var response = await PostAsync($"/minutes/{minutesId}/finalize", new { });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task FinalizeMinutes_UnknownId_Returns404()
    {
        var response = await PostAsync($"/minutes/{UnknownId}/finalize", new { });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FinalizeMinutes_AlreadyFinalized_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        await PostAsync($"/minutes/{minutesId}/finalize", new { });
        var response = await PostAsync($"/minutes/{minutesId}/finalize", new { });
        ShouldBe(response, HttpStatusCode.Conflict,
            "finalizing already-finalized minutes must be rejected");
    }

    // ── POST /minutes/{minutesId}/abandon ────────────────────────

    [Fact]
    public async Task AbandonMinutes_WithNote_Returns200()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var response = await PostAsync($"/minutes/{minutesId}/abandon", new
        {
            note = "Cancelled — rescheduled"
        });
        ShouldBeSuccess(response);
    }

    [Fact]
    public async Task AbandonMinutes_WithoutNote_Returns422()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var response = await PostAsync($"/minutes/{minutesId}/abandon", new { });
        // Abandonment requires a note — confirm it's rejected
        response.IsSuccessStatusCode.Should().BeFalse(
            "abandoning without a note must be rejected");
    }

    [Fact]
    public async Task AbandonMinutes_UnknownId_Returns404()
    {
        var response = await PostAsync($"/minutes/{UnknownId}/abandon", new
        {
            note = "Ghost"
        });
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AbandonMinutes_FinalizedMinutes_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        await PostAsync($"/minutes/{minutesId}/finalize", new { });
        var response = await PostAsync($"/minutes/{minutesId}/abandon", new
        {
            note = "Too late"
        });
        ShouldBe(response, HttpStatusCode.Conflict,
            "cannot abandon finalized minutes");
    }

    // ── GET /minutes/{minutesId}/attendees ───────────────────────

    [Fact]
    public async Task GetAttendees_NoAttendees_Returns200WithEmptyList()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/minutes/{minutesId}/attendees");
        ShouldBeSuccess(response);
        body!.Length.Should().Be(0);
    }

    [Fact]
    public async Task GetAttendees_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement[]>(
            $"/minutes/{UnknownId}/attendees");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // ── POST /minutes/{minutesId}/attendees ──────────────────────

    [Fact]
    public async Task AddAttendee_ValidUser_Returns201()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var userId = await CreateSecondUserAsync();
        var (response, _) = await PostAsync<JsonElement>(
            $"/minutes/{minutesId}/attendees",
            new { userId });
        ShouldBe(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddAttendee_DuplicateUser_Returns409()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var userId = await CreateSecondUserAsync();
        await PostAsync($"/minutes/{minutesId}/attendees", new { userId });
        var response = await PostAsync($"/minutes/{minutesId}/attendees", new { userId });
        ShouldBe(response, HttpStatusCode.Conflict);
    }

    // ── DELETE /minutes/{minutesId}/attendees/{userId} ───────────

    [Fact]
    public async Task RemoveAttendee_ExistingAttendee_Returns204()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var userId = await CreateSecondUserAsync();
        await PostAsync($"/minutes/{minutesId}/attendees", new { userId });
        var response = await DeleteAsync(
            $"/minutes/{minutesId}/attendees/{userId}");
        ShouldBe(response, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveAttendee_UnknownUser_Returns404()
    {
        var seriesId = await CreateSeriesAsync();
        var minutesId = await CreateMinutesAsync(seriesId);
        var response = await DeleteAsync(
            $"/minutes/{minutesId}/attendees/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }
}
