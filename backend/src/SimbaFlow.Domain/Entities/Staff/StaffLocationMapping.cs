using SimbaFlow.Domain.Entities.Locations;

namespace SimbaFlow.Domain.Entities.Staff;

/// <summary>
/// Maps a staff member to locations where they can operate.
/// Enables traveling staff (e.g., staff works at Office A on Mon, Branch B on Tue).
/// </summary>
public class StaffLocationMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffProfileId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public string? Schedule { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}
