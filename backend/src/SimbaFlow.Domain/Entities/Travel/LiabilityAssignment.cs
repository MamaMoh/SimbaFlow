using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Travel;

public class LiabilityAssignment : BaseEntity
{
    public Guid ExceptionCaseId { get; set; }
    public LiabilityParty Party { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ETB";
    public string? Notes { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public ExceptionCase? ExceptionCase { get; set; }
}
