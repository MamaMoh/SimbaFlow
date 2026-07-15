using System.ComponentModel.DataAnnotations;

namespace SimbaFlow.Domain.Common;

/// <summary>
/// Base entity with audit fields, soft delete, optimistic concurrency, and domain event support.
/// </summary>
public abstract class BaseEntity : IHasDomainEvents
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Mapped to PostgreSQL's xmin system column.
    /// EF Core uses this to detect concurrent modifications.
    /// </summary>
    [Timestamp]
    public uint RowVersion { get; set; }

    // Domain Events
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(IDomainEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
