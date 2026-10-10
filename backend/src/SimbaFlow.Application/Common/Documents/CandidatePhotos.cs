using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Documents;

/// <summary>
/// The two photographs a CV is drawn with.
///
/// Every place that renders a CV has to fetch these the same way, and the Telegram bot did not:
/// it rendered without them, so the CV a desk officer pulled up on their phone came out with
/// empty photo frames while the identical document from the web application had the candidate's
/// face on it.
/// </summary>
public static class CandidatePhotos
{
    public static async Task<(byte[]? Photo, byte[]? FullPhoto)> LoadAsync(
        IFileStorageService storage,
        Candidate candidate,
        CancellationToken cancellationToken = default)
    {
        return (
            await ReadAsync(storage, candidate.PhotoPath, cancellationToken),
            await ReadAsync(storage, candidate.FullPhotoPath, cancellationToken));
    }

    /// <summary>
    /// A photograph that cannot be read is not a reason to refuse the document — the CV is still
    /// wanted, with a blank frame where the picture would be.
    /// </summary>
    private static async Task<byte[]?> ReadAsync(
        IFileStorageService storage, string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            await using var stream = await storage.DownloadAsync(path, cancellationToken);
            if (stream is null) return null;
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
