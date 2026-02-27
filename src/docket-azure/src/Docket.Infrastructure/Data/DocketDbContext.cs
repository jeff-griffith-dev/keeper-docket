using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Docket.Infrastructure.Data;

public class DocketDbContext(DbContextOptions<DocketDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MeetingSeries> MeetingSeries => Set<MeetingSeries>();
    public DbSet<SeriesParticipant> SeriesParticipants => Set<SeriesParticipant>();
    public DbSet<Minutes> Minutes => Set<Minutes>();
    public DbSet<MinutesAttendee> MinutesAttendees => Set<MinutesAttendee>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<InfoItem> InfoItems => Set<InfoItem>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<ActionItemNote> ActionItemNotes => Set<ActionItemNote>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<ActionItemLabel> ActionItemLabels => Set<ActionItemLabel>();
    public DbSet<TopicLabel> TopicLabels => Set<TopicLabel>();

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocketDbContext).Assembly);
        SeedSystemLabels(modelBuilder);
    }

    private static readonly DateTimeOffset SeedEpoch =
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static void SeedSystemLabels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Label>().HasData(
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000001"), Name = "Decision", Category = LabelCategory.Action, IsSystem = true, Color = "#1565C0", CreatedAt = SeedEpoch },
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000002"), Name = "Proposal", Category = LabelCategory.Action, IsSystem = true, Color = "#6A1B9A", CreatedAt = SeedEpoch },
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000003"), Name = "New", Category = LabelCategory.Action, IsSystem = true, Color = "#37474F", CreatedAt = SeedEpoch },
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000004"), Name = "Status:GREEN", Category = LabelCategory.Status, IsSystem = true, Color = "#2E7D32", CreatedAt = SeedEpoch },
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000005"), Name = "Status:YELLOW", Category = LabelCategory.Status, IsSystem = true, Color = "#F57F17", CreatedAt = SeedEpoch },
            new Label { Id = new Guid("10000000-0000-0000-0000-000000000006"), Name = "Status:RED", Category = LabelCategory.Status, IsSystem = true, Color = "#B71C1C", CreatedAt = SeedEpoch }
        );
    }
}