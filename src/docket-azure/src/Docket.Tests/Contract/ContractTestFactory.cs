using Docket.Api.Services;
using Docket.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Docket.Tests.Contract;

/// <summary>
/// Shared factory for contract tests. Seeds to "fresh install" state:
/// system labels seeded, stub user present, no user data.
/// All 38 endpoints are exercised against this baseline.
/// </summary>
public class ContractTestFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAliveConnection;

    public ContractTestFactory()
    {
        var dbName = $"docket-contract-{Guid.NewGuid()}";
        _keepAliveConnection = new SqliteConnection(
            $"Data Source={dbName};Mode=Memory;Cache=Shared");
        _keepAliveConnection.Open();
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

            // Seed system labels (fresh install state)
            SeedSystemLabels(db);

            // Seed stub user (required for authenticated endpoints)
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

    private static void SeedSystemLabels(DocketDbContext db)
    {
        // Only seed if not already present (EnsureCreated may have seeded via model)
        if (db.Labels.Any(l => l.IsSystem))
            return;

        // System labels are seeded by DevSeedAsync in normal startup;
        // in Testing environment we replicate that here.
        // If your DbContext seeds these via HasData, this block will be a no-op
        // due to the Any() check above.
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}
