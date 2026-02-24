using Docket.Domain.Enums;

namespace Docket.Domain.Entities;

/// <summary>
/// Classification tag for ActionItems and Topics.
/// Two categories:
///   Action — classifies type of commitment: Decision, Proposal, New
///   Status — traffic-light health indicator: Status:GREEN, Status:YELLOW, Status:RED
///
/// System labels are seeded on install and cannot be deleted.
/// Custom labels can be created by any authenticated user.
/// </summary>
public class Label
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public LabelCategory Category { get; set; }
    public string? Color { get; set; }

    /// <summary>
    /// System labels are seeded on install. Cannot be deleted.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation
    public ICollection<ActionItemLabel> ActionItemLabels { get; set; } = [];
    public ICollection<TopicLabel> TopicLabels { get; set; } = [];
}

/// <summary>
/// Junction table: many-to-many between ActionItem and Label.
/// A single ActionItem can carry Decision + Proposal + New simultaneously —
/// confirmed from 4Minitz archaeology.
/// </summary>
public class ActionItemLabel
{
    public Guid ActionItemId { get; set; }
    public Guid LabelId { get; set; }

    // Navigation
    public ActionItem? ActionItem { get; set; }
    public Label? Label { get; set; }
}

/// <summary>
/// Junction table: many-to-many between Topic and Label.
/// Topics can carry Status labels independently of their ActionItems.
/// </summary>
public class TopicLabel
{
    public Guid TopicId { get; set; }
    public Guid LabelId { get; set; }

    // Navigation
    public Topic? Topic { get; set; }
    public Label? Label { get; set; }
}
