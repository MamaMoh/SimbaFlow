using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Audit;

/// <summary>
/// An unhandled failure, kept so it can be found without a customer reporting it.
///
/// Errors were only visible in container logs, which nobody reads until something is already
/// known to be wrong — the broken documents tab (uploads succeeding, listing answering 405) sat
/// in production unnoticed because nothing surfaced it.
///
/// Rows are grouped by Fingerprint so one recurring fault reads as a single problem with a count
/// rather than a thousand lines.
/// </summary>
public class ErrorEvent : BaseEntity
{
    /// <summary>Stable hash of type + message + top frame; identical faults share it.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>"api" or "web" — a browser crash and a server crash need the same triage.</summary>
    public string Source { get; set; } = "api";

    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }

    public string? Path { get; set; }
    public string? Method { get; set; }
    public int? StatusCode { get; set; }

    /// <summary>Who hit it, when known — enough to follow up, no more.</summary>
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when someone has dealt with it, so the list shows what still needs attention.</summary>
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
