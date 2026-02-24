namespace Docket.Domain.Entities;

/// <summary>
/// Discussion note, status update, or decision record within a Topic.
/// Informational — not a commitment. Does not carry forward independently.
/// Belongs to the Topic instance it was created on.
/// </summary>
public class InfoItem : EntityBase
{
    public Guid TopicId { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// If set, this item is rendered as a dated bullet in finalized minutes output.
    /// </summary>
    public DateOnly? PinnedDate { get; set; }

    public Guid CreatedBy { get; set; }

    // Navigation
    public Topic? Topic { get; set; }
    public User? CreatedByUser { get; set; }
}
