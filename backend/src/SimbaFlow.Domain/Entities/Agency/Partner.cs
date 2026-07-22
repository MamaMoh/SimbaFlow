using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Agency;

/// <summary>
/// Overseas partner agency / sponsor directory entry.
/// </summary>
public class Partner : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameArabic { get; set; }
    public string? SponsorId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
