using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Docket.Tests.Contract;

// ────────────────────────────────────────────────────────────────
// GET /health
// ────────────────────────────────────────────────────────────────
public class HealthContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await Client.GetAsync("/health");
        ShouldBeSuccess(response, "health endpoint must always return 200");
    }
}

// ────────────────────────────────────────────────────────────────
// GET  /labels/
// POST /labels/
// DELETE /labels/{labelId}
// ────────────────────────────────────────────────────────────────
public class LabelContractTests(ContractTestFactory factory)
    : ContractTestBase(factory)
{
    // GET /labels/ — system labels exist on fresh install
    [Fact]
    public async Task GetLabels_Returns200WithSystemLabels()
    {
        var (response, body) = await GetAsync<JsonElement[]>("/labels/");
        ShouldBeSuccess(response);
        body.Should().NotBeNull();
        body!.Length.Should().BeGreaterThan(0, "system labels are seeded on fresh install");
    }

    // POST /labels/ — create a custom label
    [Fact]
    public async Task CreateLabel_ValidBody_Returns201()
    {
        var (response, _) = await PostAsync<JsonElement>("/labels/", new
        {
            name = $"Contract-Label-{Guid.NewGuid():N}",
            category = "Action",
            color = "#FF0000"
        });
        ShouldBe(response, HttpStatusCode.Created);
    }

    // POST /labels/ — missing required name returns 400
    [Fact]
    public async Task CreateLabel_MissingName_Returns400()
    {
        var response = await PostAsync("/labels/", new
        {
            category = "Custom"
        });
        ShouldBe(response, HttpStatusCode.BadRequest);
    }

    // DELETE /labels/{labelId} — unknown id returns 404
    [Fact]
    public async Task DeleteLabel_UnknownId_Returns404()
    {
        var response = await DeleteAsync($"/labels/{UnknownId}");
        ShouldBe(response, HttpStatusCode.NotFound);
    }

    // DELETE /labels/{labelId} — system labels cannot be deleted (409 or 403)
    [Fact]
    public async Task DeleteLabel_SystemLabel_IsRejected()
    {
        // Get a system label id first
        var (_, labels) = await GetAsync<JsonElement[]>("/labels/");
        var systemLabel = labels!.FirstOrDefault(l =>
            l.TryGetProperty("isSystem", out var v) && v.GetBoolean());
        systemLabel.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "at least one system label must exist");

        var id = systemLabel.GetProperty("id").GetString();
        var response = await DeleteAsync($"/labels/{id}");
        ((int)response.StatusCode).Should().Be(403); // OneOf(409, 403,"system labels must be protected from deletion");
    }
}
