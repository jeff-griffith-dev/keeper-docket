using System.Net;
using System.Text.Json;
using Docket.Api.Services;
using FluentAssertions;
using Xunit;

namespace Docket.Tests.Contract;

// ────────────────────────────────────────────────────────────────
// POST  /users/
// GET   /users/{userId}
// PATCH /users/{userId}
// GET   /users/{userId}/open-items
// ────────────────────────────────────────────────────────────────
public class UserContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // POST /users/ — create a new user
    [Fact]
    public async Task CreateUser_ValidBody_Returns201()
    {
        var (response, body) = await PostAsync<JsonElement>("/users/", new
        {
            email = $"contract-{Guid.NewGuid():N}@test.local",
            displayName = "Contract Test User"
        });
        ShouldBe(response, HttpStatusCode.Created);
        body.TryGetProperty("id", out _).Should().BeTrue("response must include id");
    }

    // POST /users/ — missing email returns 400
    [Fact]
    public async Task CreateUser_MissingEmail_Returns400()
    {
        var response = await PostAsync("/users/", new
        {
            displayName = "No Email User"
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    // POST /users/ — duplicate email returns 409
    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        await PostAsync("/users/", new { email, displayName = "First" });
        var response = await PostAsync("/users/", new { email, displayName = "Second" });
        ShouldBe(response, HttpStatusCode.Conflict);
    }

    // GET /users/{userId} — stub user exists
    [Fact]
    public async Task GetUser_ExistingId_Returns200()
    {
        var (response, body) = await GetAsync<JsonElement>(
            $"/users/{StubCurrentUserService.StubUserId}");
        ShouldBeSuccess(response);
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    // GET /users/{userId} — unknown id returns 404
    [Fact]
    public async Task GetUser_UnknownId_Returns404()
    {
        var (response, _) = await GetAsync<JsonElement>($"/users/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // PATCH /users/{userId} — update display name
    [Fact]
    public async Task UpdateUser_ValidBody_Returns200()
    {
        await GetUser_ExistingId_Returns200(); // ensure stub user exists before updating

        var response = await PatchAsync(
            $"/users/{StubCurrentUserService.StubUserId}",
            new { displayName = "Updated Name" });
        ShouldBeSuccess(response);
    }

    // PATCH /users/{userId} — unknown id returns 403 because you can only update your own user record
    [Fact]
    public async Task UpdateUser_UnknownId_Returns403()
    {
        var response = await PatchAsync($"/users/{UnknownId}",
            new { displayName = "Ghost" });
        ShouldBe(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUser_OtherUser_Returns403()
    {
        var (_, body) = await PostAsync<JsonElement>("/users/", new
        {
            email = $"other-{Guid.NewGuid():N}@test.local",
            displayName = "Other User"
        });
        var otherId = Guid.Parse(body.GetProperty("id").GetString()!);
        var response = await PatchAsync($"/users/{otherId}",
            new { displayName = "Hacked", externalId = "ext-hack" });
        ShouldBe(response, HttpStatusCode.Forbidden);
    }
    [Fact]
    public async Task GetOpenItems_Returns200WithList()
    {
        var (response, body) = await GetAsync<JsonElement[]>(
            $"/users/{StubCurrentUserService.StubUserId}/open-items");
        ShouldBeSuccess(response);
        body.Should().NotBeNull("response must be a list, even if empty");
    }

    // GET /users/{userId}/open-items — unknown user returns 403
    [Fact]
    public async Task GetOpenItems_UnknownUser_Returns403()
    {
        var (response, _) = await GetAsync<JsonElement[]>(
            $"/users/{UnknownId}/open-items");
        ShouldBe(response, HttpStatusCode.Forbidden);
    }

    // GET /users/{userId}/open-items — other user's items returns 403
    [Fact]
    public async Task GetOpenItems_OtherUser_Returns403()
    {
        // Create a second user, then try to fetch their open items as the stub user
        var (_, body) = await PostAsync<JsonElement>("/users/", new
        {
            email = $"other-{Guid.NewGuid():N}@test.local",
            displayName = "Other User"
        });
        var otherId = Guid.Parse(body.GetProperty("id").GetString()!);

        var (response, _) = await GetAsync<JsonElement[]>($"/users/{otherId}/open-items");
        ShouldBe(response, HttpStatusCode.Forbidden);
    }
}
