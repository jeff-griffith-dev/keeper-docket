using Docket.Domain.Entities;
using Docket.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Docket.Infrastructure.Data.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(255).IsRequired();
        builder.Property(u => u.ExternalId).HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
    }
}

public class MeetingSeriesConfiguration : IEntityTypeConfiguration<MeetingSeries>
{
    public void Configure(EntityTypeBuilder<MeetingSeries> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(255).IsRequired();
        builder.Property(s => s.Project).HasMaxLength(255);
        builder.Property(s => s.ExternalCalendarId).HasMaxLength(500);
        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(s => s.CreatedByUser)
            .WithMany()
            .HasForeignKey(s => s.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Participants)
            .WithOne(p => p.Series)
            .HasForeignKey(p => p.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Minutes)
            .WithOne(m => m.Series)
            .HasForeignKey(m => m.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SeriesParticipantConfiguration : IEntityTypeConfiguration<SeriesParticipant>
{
    public void Configure(EntityTypeBuilder<SeriesParticipant> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // A user can hold only one role per series
        builder.HasIndex(p => new { p.SeriesId, p.UserId }).IsUnique();

        builder.HasOne(p => p.User)
            .WithMany(u => u.Participations)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MinutesConfiguration : IEntityTypeConfiguration<Minutes>
{
    public void Configure(EntityTypeBuilder<Minutes> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Self-referencing linked list
        builder.HasOne(m => m.PreviousMinutes)
            .WithMany()
            .HasForeignKey(m => m.PreviousMinutesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.FinalizedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.AbandonedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(m => m.Topics)
            .WithOne(t => t.Minutes)
            .HasForeignKey(t => t.MinutesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Attendees)
            .WithOne(a => a.Minutes)
            .HasForeignKey(a => a.MinutesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.SeriesId);
        builder.HasIndex(m => m.ScheduledFor);
    }
}

public class MinutesAttendeeConfiguration : IEntityTypeConfiguration<MinutesAttendee>
{
    public void Configure(EntityTypeBuilder<MinutesAttendee> builder)
    {
        // Composite PK — no surrogate key needed
        builder.HasKey(a => new { a.MinutesId, a.UserId });

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).HasMaxLength(255).IsRequired();
        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Self-referencing carry-forward chain
        builder.HasOne(t => t.SourceTopic)
            .WithMany()
            .HasForeignKey(t => t.SourceTopicId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Responsible)
            .WithMany()
            .HasForeignKey(t => t.ResponsibleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.InfoItems)
            .WithOne(i => i.Topic)
            .HasForeignKey(i => i.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ActionItems)
            .WithOne(a => a.Topic)
            .HasForeignKey(a => a.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InfoItemConfiguration : IEntityTypeConfiguration<InfoItem>
{
    public void Configure(EntityTypeBuilder<InfoItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.HasOne(i => i.CreatedByUser)
            .WithMany()
            .HasForeignKey(i => i.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ActionItemConfiguration : IEntityTypeConfiguration<ActionItem>
{
    public void Configure(EntityTypeBuilder<ActionItem> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).HasMaxLength(500).IsRequired();
        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(a => a.Priority).IsRequired();

        // Self-referencing carry-forward chain
        builder.HasOne(a => a.SourceActionItem)
            .WithMany()
            .HasForeignKey(a => a.SourceActionItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Responsible)
            .WithMany(u => u.OwnedActionItems)
            .HasForeignKey(a => a.ResponsibleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser)
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Notes)
            .WithOne(n => n.ActionItem)
            .HasForeignKey(n => n.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ResponsibleId);
        builder.HasIndex(a => a.Status);
    }
}

public class ActionItemNoteConfiguration : IEntityTypeConfiguration<ActionItemNote>
{
    public void Configure(EntityTypeBuilder<ActionItemNote> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Phase)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Enforce append-only at the DB level — no updates allowed
        // (enforced at application layer; DB constraint is a safety net)
        builder.HasIndex(n => new { n.ActionItemId, n.CreatedAt });
    }
}

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Color).HasMaxLength(7);
        builder.Property(l => l.Category)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.HasIndex(l => l.Name).IsUnique();
    }
}

public class ActionItemLabelConfiguration : IEntityTypeConfiguration<ActionItemLabel>
{
    public void Configure(EntityTypeBuilder<ActionItemLabel> builder)
    {
        builder.HasKey(al => new { al.ActionItemId, al.LabelId });

        builder.HasOne(al => al.ActionItem)
            .WithMany(a => a.ActionItemLabels)
            .HasForeignKey(al => al.ActionItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(al => al.Label)
            .WithMany(l => l.ActionItemLabels)
            .HasForeignKey(al => al.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TopicLabelConfiguration : IEntityTypeConfiguration<TopicLabel>
{
    public void Configure(EntityTypeBuilder<TopicLabel> builder)
    {
        builder.HasKey(tl => new { tl.TopicId, tl.LabelId });

        builder.HasOne(tl => tl.Topic)
            .WithMany(t => t.TopicLabels)
            .HasForeignKey(tl => tl.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tl => tl.Label)
            .WithMany(l => l.TopicLabels)
            .HasForeignKey(tl => tl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
