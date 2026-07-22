using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Agency;

/// <summary>
/// Agency branch (office) within a tenant. Candidates are registered against an office.
/// </summary>
public class Office : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
