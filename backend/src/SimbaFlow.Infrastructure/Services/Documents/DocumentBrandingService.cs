using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

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
        // The partner's branding wins: the paperwork is presented under their name. Within each
        // organisation the printed letterhead is preferred, and the logo stands in when they have
        // only uploaded a mark — better a small logo at the top than nothing.
        if (candidate.PartnerAgencyId is Guid partnerId)
        {
            var partner = await _platform.PartnerAgencies
                .AsNoTracking()
                .Where(p => p.Id == partnerId && !p.IsDeleted)
                .Select(p => new { p.LetterheadPath, p.LogoPath })
                .FirstOrDefaultAsync(cancellationToken);

            var bytes = await ReadAsync(partner?.LetterheadPath, cancellationToken)
                ?? await ReadAsync(partner?.LogoPath, cancellationToken);
            if (bytes is not null) return bytes;
        }

        // No partner, or the partner has no branding at all — fall back to the agency's own.
        if (_currentUser.TenantId is Guid tenantId)
        {
            var agency = await _platform.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => new { t.LetterheadPath, t.LogoPath })
                .FirstOrDefaultAsync(cancellationToken);

            return await ReadAsync(agency?.LetterheadPath, cancellationToken)
                ?? await ReadAsync(agency?.LogoPath, cancellationToken);
        }

        return null;
    }

    public async Task<string> GetCvTemplateAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return CvTemplates.Default;

        var settings = await _platform.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => t.Settings)
            .FirstOrDefaultAsync(cancellationToken);

        return CvTemplates.Normalise(settings?.Documents.CvTemplate);
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
