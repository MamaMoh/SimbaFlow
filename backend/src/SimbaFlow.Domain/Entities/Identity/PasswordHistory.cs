namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Stores previous password hashes to prevent password reuse.
/// Configurable history depth (default: last 5 passwords).
/// </summary>
public class PasswordHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
