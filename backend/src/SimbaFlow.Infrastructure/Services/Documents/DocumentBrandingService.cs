using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <inheritdoc />
public sealed class DocumentBrandingService : IDocumentBrandingService
{
    private readonly IPlatformDbContext _platform;
    private readonly IFileStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<DocumentBrandingService> _logger;

    public DocumentBrandingService(
        IPlatformDbContext platform,
        IFileStorageService storage,
        ICurrentUserService currentUser,
        ILogger<DocumentBrandingService> logger)
    {
        _platform = platform;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<byte[]?> GetHeaderLogoAsync(
        Candidate candidate, CancellationToken cancellationToken = default)
    {
        // The partner's letterhead wins: the paperwork is presented under their name.
        if (candidate.PartnerAgencyId is Guid partnerId)
        {
            var partnerLogo = await _platform.PartnerAgencies
                .AsNoTracking()
                .Where(p => p.Id == partnerId && !p.IsDeleted)
                .Select(p => p.LogoPath)
                .FirstOrDefaultAsync(cancellationToken);

            var bytes = await ReadAsync(partnerLogo, cancellationToken);
            if (bytes is not null) return bytes;
        }

        // No partner, or the partner never uploaded one — fall back to the agency's own.
        if (_currentUser.TenantId is Guid tenantId)
        {
            var agencyLogo = await _platform.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => t.LogoPath)
                .FirstOrDefaultAsync(cancellationToken);

            return await ReadAsync(agencyLogo, cancellationToken);
        }

        return null;
    }

    private async Task<byte[]?> ReadAsync(string? relativePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        try
        {
            await using var stream = await _storage.DownloadAsync(relativePath, cancellationToken);
            if (stream is null) return null;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (Exception ex)
        {
            // A missing or unreadable logo must not stop the document being produced — the header
            // falls back to the printed agency name.
            _logger.LogWarning(ex, "Could not read logo {Path}; falling back to the printed name", relativePath);
            return null;
        }
    }
}
