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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<DocketDbContext>(options =>
        options.UseSqlite(connectionString ?? "Data Source=docket-dev.db")
               .AddInterceptors(new AuditInterceptor())
               .EnableSensitiveDataLogging()
               .EnableDetailedErrors());
}
else
{
    builder.Services.AddDbContext<DocketDbContext>(options =>
        options.UseSqlServer(connectionString
            ?? throw new InvalidOperationException(
                "DefaultConnection connection string is required in non-development environments."))
               .AddInterceptors(new AuditInterceptor()));
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

    // Auto-apply migrations and seed the dev database on startup
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DocketDbContext>();
    await db.Database.MigrateAsync();
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
