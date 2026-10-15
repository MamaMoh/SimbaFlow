namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Marks a request whose successful handling shows someone personal records, so
/// <c>ReadAuditBehavior</c> writes who read what to the read-audit log.
/// </summary>
public interface IAuditedRead
{
    /// <summary>What kind of record is read (e.g., "Candidate").</summary>
    string AuditEntityType { get; }

    /// <summary>
    /// The records read, one audit entry each. Empty for a list or search, which is logged once
    /// without an id.
    /// </summary>
    IReadOnlyCollection<Guid> AuditEntityIds { get; }
}
