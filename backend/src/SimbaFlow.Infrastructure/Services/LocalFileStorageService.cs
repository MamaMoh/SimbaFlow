using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// File storage implementation using the local file system.
/// Files are organized in tenant-specific directories.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;
    private static readonly HashSet<string> AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".docx"];
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png"];
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int ThumbnailSize = 200;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        var configured = configuration["FileStorage:BasePath"];
        _basePath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Directory.GetCurrentDirectory(), "storage")
                : configured);
        Directory.CreateDirectory(_basePath);
        _logger = logger;
        _logger.LogInformation("File storage base path: {BasePath}", _basePath);
    }

    public async Task<string> UploadAsync(
        string tenantSlug, Guid candidateId, string fileName,
        string contentType, Stream fileStream, CancellationToken cancellationToken = default)
    {
        // Validate file extension
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

        // Validate file size
        if (fileStream.Length > MaxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds the maximum of {MaxFileSizeBytes / 1024 / 1024}MB.");

        // Build safe path
        var directory = Path.Combine(_basePath, "tenants", tenantSlug, "candidates", candidateId.ToString());
        Directory.CreateDirectory(directory);

        var uniqueFileName = $"{Guid.NewGuid():N}_{SanitizeFileName(fileName)}";
        var fullPath = Path.Combine(directory, uniqueFileName);
        var relativePath = Path.Combine("tenants", tenantSlug, "candidates", candidateId.ToString(), uniqueFileName);

        // Write file
        await using var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(outputStream, cancellationToken);

        _logger.LogInformation("File uploaded: {RelativePath} ({Size} bytes)", relativePath, fileStream.Length);

        // Generate thumbnail for images
        if (ImageExtensions.Contains(extension))
        {
            await GenerateThumbnailAsync(fullPath, cancellationToken);
        }

        return relativePath;
    }

    public Task<Stream?> DownloadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);

        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        // Soft delete — file remains on disk, DB record marks as deleted
        _logger.LogInformation("File marked for deletion: {RelativePath}", relativePath);
        return Task.CompletedTask;
    }

    public Task<string?> GetThumbnailAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(relativePath).ToLowerInvariant();
        if (!ImageExtensions.Contains(extension))
            return Task.FromResult<string?>(null);

        var thumbPath = GetThumbnailPath(relativePath);
        var fullThumbPath = GetFullPath(thumbPath);

        return Task.FromResult<string?>(File.Exists(fullThumbPath) ? thumbPath : null);
    }

    private async Task GenerateThumbnailAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            var thumbPath = GetThumbnailPath(fullPath);

            using var image = await Image.LoadAsync(fullPath, cancellationToken);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ThumbnailSize, ThumbnailSize),
                Mode = ResizeMode.Max
            }));

            await image.SaveAsJpegAsync(thumbPath, cancellationToken);
            _logger.LogDebug("Thumbnail generated: {ThumbPath}", thumbPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate thumbnail for {Path}", fullPath);
        }
    }

    private string GetFullPath(string relativePath)
    {
        // Prevent path traversal
        var normalized = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        if (!normalized.StartsWith(Path.GetFullPath(_basePath)))
            throw new InvalidOperationException("Invalid file path — access denied.");

        return normalized;
    }

    private static string GetThumbnailPath(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        return Path.Combine(dir, $"{name}_thumb.jpg");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }
}
