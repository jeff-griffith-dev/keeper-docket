using Docket.Api.Services;
using Docket.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Docket.Tests.Integration;

/// <summary>
/// Integration tests for /users endpoints.
/// Uses WebApplicationFactory with an isolated in-memory SQLite database per test.
/// </summary>
public class UserEndpointTests : IClassFixture<DocketWebApplicationFactory>
{
    private readonly DocketWebApplicationFactory _factory;

    public UserEndpointTests(DocketWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateUser_ValidRequest_Returns201WithUser()
    {
        var client = _factory.CreateClient();
        var request = new { email = "alice@example.com", displayName = "Alice" };

        var response = await client.PostAsJsonAsync("/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body.Should().NotBeNull();
        body!.Email.Should().Be("alice@example.com");
        body.DisplayName.Should().Be("Alice");
        body.Id.Should().NotBeEmpty();
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUser_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var request = new { email = "bob@example.com", displayName = "Bob" };

        await client.PostAsJsonAsync("/users", request);
        var second = await client.PostAsJsonAsync("/users", request);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task CreateUser_EmailNormalisedToLowercase()
    {
        var client = _factory.CreateClient();
        var request = new { email = "Carol@Example.COM", displayName = "Carol" };

        var response = await client.PostAsJsonAsync("/users", request);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();

        body!.Email.Should().Be("carol@example.com");
    }

    [Fact]
    public async Task GetUser_ExistingUser_Returns200()
    {
        var client = _factory.CreateClient();

        // The stub user is always seeded
        var response = await client.GetAsync($"/users/{StubCurrentUserService.StubUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserDto>();
        body!.Id.Should().Be(StubCurrentUserService.StubUserId);
    }

    [Fact]
    public async Task GetUser_NonExistentUser_Returns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOpenItems_NoItems_ReturnsEmptyArray()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/users/{StubCurrentUserService.StubUserId}/open-items");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<object>>();
        items.Should().BeEmpty();
    }

    // Simple DTOs for deserializing responses in tests
    private record UserDto(Guid Id, string Email, string DisplayName, string? ExternalId);
    private record ProblemDto(string Title, string Detail, int Status);
}

/// <summary>
/// Test factory that replaces the database with an isolated SQLite in-memory
/// instance per factory instance. Each test class gets a fresh database.
/// </summary>
public class DocketWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<DocketDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Replace with isolated SQLite in-memory database
            var dbName = $"docket-test-{Guid.NewGuid()}";
            services.AddDbContext<DocketDbContext>(options =>
                options.UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared"));

            // Build the database schema and seed the stub user
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DocketDbContext>();
            db.Database.EnsureCreated();

            // Seed stub user for auth
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
}
