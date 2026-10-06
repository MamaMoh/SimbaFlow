using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// A skill this agency offers on the candidate form. Built-in rows map to Candidate boolean
/// columns; agencies may add their own names beside those.
/// </summary>
public class AgencySkill : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Stable key for built-in skills (cleaning, washing, …). Null for agency-added skills.
    /// </summary>
    public string? BuiltInKey { get; set; }

    public bool IsBuiltIn { get; set; }
    public bool IsDefaultSelected { get; set; }
    public int SortOrder { get; set; }
}
