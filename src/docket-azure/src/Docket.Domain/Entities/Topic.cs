using Docket.Domain.Enums;

namespace Docket.Domain.Entities;

/// <summary>
/// An agenda item within a Minutes. Topics are the unit of carry-forward.
///
/// Recurring topics carry forward automatically when new Minutes are created,
/// if they are still open at the time of the prior Minutes' finalization.
/// Carry-forward creates a new Topic record with SourceTopicId pointing back
/// to the originating topic — preserving the chain while allowing the new
/// instance to accumulate its own InfoItems and ActionItems.
///
/// isOpen/isSkipped combinations:
///   isOpen:true,  isSkipped:false — open, under active discussion
///   isOpen:true,  isSkipped:true  — explicitly skipped this meeting, carries forward
///   isOpen:false, isSkipped:false — resolved and closed
///   isOpen:false, isSkipped:true  — INVALID (closed topic cannot be skipped)
/// </summary>
public class Topic : EntityBase
{
    public Guid MinutesId { get; set; }

    /// <summary>
    /// If this topic was carried forward from a prior Minutes, points to
    /// the originating Topic record. Null for original topics.
    /// </summary>
    public Guid? SourceTopicId { get; set; }

    public string Title { get; set; } = string.Empty;
    public TopicType Type { get; set; } = TopicType.Adhoc;
    public bool IsOpen { get; set; } = true;
    public bool IsSkipped { get; set; } = false;
    public int SortOrder { get; set; }
    public Guid? ResponsibleId { get; set; }

    // Navigation
    public Minutes? Minutes { get; set; }
    public Topic? SourceTopic { get; set; }
    public User? Responsible { get; set; }
    public ICollection<InfoItem> InfoItems { get; set; } = [];
    public ICollection<ActionItem> ActionItems { get; set; } = [];
    public ICollection<TopicLabel> TopicLabels { get; set; } = [];
}
