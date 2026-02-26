using Docket.Api.Endpoints;
using Docket.Api.Middleware;
using Docket.Api.Services;
using Docket.Infrastructure.Data;
using Docket.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database — SQLite for dev, Azure SQL for prod
// ---------------------------------------------------------------------------

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<DocketDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=docket-dev.db"));

    //builder.Services.AddDbContext<DocketDbContext>(options =>
    //    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
    //        ?? "Data Source=docket-dev.db")
    //           .AddInterceptors(new AuditInterceptor())
    //           .EnableSensitiveDataLogging()
    //           .EnableDetailedErrors());
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection connection string is required in non-development environments.");
    builder.Services.AddDbContext<DocketDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// ---------------------------------------------------------------------------
// Authentication — stub for v1, swap for JWT in production
// ---------------------------------------------------------------------------
builder.Services.AddScoped<ICurrentUserService, StubCurrentUserService>();

// ---------------------------------------------------------------------------
// API
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Docket API";
        options.Theme = ScalarTheme.Default;
    });

    // Auto-apply schema and seed on startup.
    // SQLite (dev local + tests) uses EnsureCreated — no migration history table needed.
    // SQL Server (prod) uses MigrateAsync — incremental migrations applied in order.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DocketDbContext>();
    if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    // Only seed in true dev mode, not test mode
    if (!app.Environment.IsEnvironment("Testing"))
        await DevSeedAsync(db);
}

app.UseHttpsRedirection();

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------
app.MapUserEndpoints();
app.MapSeriesEndpoints();
app.MapMinutesEndpoints();
app.MapTopicEndpoints();
app.MapInfoItemEndpoints();
app.MapActionItemEndpoints();
app.MapLabelEndpoints();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .WithTags("health")
    .WithName("HealthCheck");

app.Run();

// ---------------------------------------------------------------------------
// Dev seed — creates the stub user so StubCurrentUserService resolves
// ---------------------------------------------------------------------------
static async Task DevSeedAsync(DocketDbContext db)
{
    if (!await db.Users.AnyAsync(u => u.Id == StubCurrentUserService.StubUserId))
    {
        db.Users.Add(new Docket.Domain.Entities.User
        {
            Id = StubCurrentUserService.StubUserId,
            Email = "dev@docket.local",
            DisplayName = "Dev User (Stub)"
        });
        await db.SaveChangesAsync();
    }
}

// Expose for integration testing
public partial class Program { }
