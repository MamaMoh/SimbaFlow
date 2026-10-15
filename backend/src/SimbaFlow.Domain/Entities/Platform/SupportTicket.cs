using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Platform;

public enum SupportTicketStatus
{
    /// <summary>Waiting on the platform team.</summary>
    Open = 0,

    /// <summary>The platform team replied; waiting on the agency.</summary>
    Answered = 1,

    Closed = 2,
}

/// <summary>
/// A help request from an agency to the platform team.
///
/// Kept in the public schema with the agency's id so the platform's inbox is one query across
/// every agency. Anyone in the agency can see and add to their agency's requests.
/// </summary>
public class SupportTicket : BaseEntity
{
    public Guid TenantId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    /// <summary>The account that raised it.</summary>
    public Guid RaisedByUserId { get; set; }

    /// <summary>When anyone last wrote on it; the inbox is sorted on this.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public List<SupportMessage> Messages { get; set; } = [];
}

public class SupportMessage : BaseEntity
{
    public Guid TicketId { get; set; }

    public SupportTicket? Ticket { get; set; }

    public Guid AuthorUserId { get; set; }

    /// <summary>True when written by the platform team rather than the agency.</summary>
    public bool FromPlatform { get; set; }

    public string Body { get; set; } = string.Empty;
}
