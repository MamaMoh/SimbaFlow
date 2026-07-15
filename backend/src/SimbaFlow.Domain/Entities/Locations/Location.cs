using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Locations;

/// <summary>
/// Represents a physical location in the hospital hierarchy.
/// Locations form a tree: Hospital → Building → Floor → Wing → Department → Ward → Room → Bed
/// </summary>
public class Location : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public LocationType Type { get; set; }
    public string? Description { get; set; }
    public Guid? ParentLocationId { get; set; }
    public Location? ParentLocation { get; set; }
    public bool IsActive { get; set; } = true;
    public int? Capacity { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<Location> ChildLocations { get; set; } = [];
    public ICollection<StaffLocationMapping> StaffMappings { get; set; } = [];
}
