namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Abstraction for file storage operations.
/// Implementations handle tenant-isolated directory management.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Upload a file and return the relative storage path.</summary>
    Task<string> UploadAsync(string tenantSlug, Guid candidateId, string fileName, string contentType, Stream fileStream, CancellationToken cancellationToken = default);

    /// <summary>Download a file by its relative storage path.</summary>
    Task<Stream?> DownloadAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Check if a file exists.</summary>
    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Delete a file (soft — marks deleted but retains on disk).</summary>
    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Get thumbnail path for an image file (generates if not exists).</summary>
    Task<string?> GetThumbnailAsync(string relativePath, CancellationToken cancellationToken = default);
}
