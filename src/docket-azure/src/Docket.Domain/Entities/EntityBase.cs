namespace Docket.Domain.Entities;

/// <summary>
/// Base class for all Docket entities. Provides Id and audit timestamps.
/// CreatedAt and UpdatedAt are managed by the Infrastructure layer via
/// EF Core interceptors — domain code does not set them directly.
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
