using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// This candidate's contract.
///
/// Drawn from the candidate, their partner agency and the agency's licence — unless one has
/// already been signed and filed, in which case that is the contract and this hands it back
/// untouched. A Saudi placement runs on a contract the parties sign, which arrives as an upload;
/// redrawing our version of it would give the desk a document with no signatures on it, differing
/// from the real one in whatever has been edited since.
/// </summary>
public record GenerateContractCommand(Guid CandidateId)
    : IRequest<Result<GeneratedPdf>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateContractHandler : IRequestHandler<GenerateContractCommand, Result<GeneratedPdf>>
{
    private readonly ITenantDbContext _tenant;
    private readonly IPlatformDbContext _platform;
    private readonly IContractGenerationService _contracts;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly ITenantContext _tenantContext;

    public GenerateContractHandler(
        ITenantDbContext tenant,
        IPlatformDbContext platform,
        IContractGenerationService contracts,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage,
        ITenantContext tenantContext)
    {
        _tenant = tenant;
        _platform = platform;
        _contracts = contracts;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
    }

    public async Task<Result<GeneratedPdf>> Handle(GenerateContractCommand request, CancellationToken ct)
    {
        var candidate = await _tenant.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<GeneratedPdf>.Failure("Candidate not found", 404);

        var signed = await SignedContractAsync(candidate, ct);
        if (signed is not null) return Result<GeneratedPdf>.Success(signed);

        var downloadName = DocumentFileName.For(candidate.FullName, "Contract");

        // The Saudi side comes from the linked partner; the Ethiopian side from the agency itself.
        var partner = candidate.PartnerAgencyId is Guid pid
            ? await _platform.PartnerAgencies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == pid && !p.IsDeleted, ct)
            : null;

        var tenant = _currentUser.TenantId is Guid tid
            ? await _platform.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tid && !t.IsDeleted, ct)
            : null;

        if (partner is null && string.IsNullOrWhiteSpace(candidate.PartnerName))
            return Result<GeneratedPdf>.Failure(
                "Select the foreign partner on the candidate before generating the contract.", 400);

        var parties = new ContractParties(
            SaudiAgencyName: partner?.Name ?? candidate.PartnerName ?? "—",
            SaudiLicenseNo: partner?.ForeignLicenseId,
            SaudiPhone: partner?.ContactPhone,
            SaudiAddress: partner?.Address,
            SaudiCity: partner?.CountryName,
            SaudiEmail: partner?.ContactEmail,
            EthiopianAgencyName: tenant?.Name ?? "—",
            EthiopianLicenseNo: tenant?.LicenseNumber,
            EthiopianAddress: tenant?.Address,
            EthiopianCity: tenant?.City,
            EthiopianPhone: tenant?.ContactPhone,
            EthiopianEmail: tenant?.ContactEmail);

        var pdf = await _contracts.GenerateAsync(candidate, parties, ct);

        // File it against the candidate. This is the document that gets signed and stamped, so it
        // has to be retrievable later — not just downloaded once by whoever clicked generate.
        await GeneratedDocuments.ReplaceAsync(
            _tenant, _fileStorage, _currentUser, _tenantContext.SchemaName ?? "default", candidate,
            DocumentType.Contract,
            $"contract_{candidate.PassportNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            downloadName,
            pdf,
            ct);
        await _tenant.SaveChangesAsync(ct);

        return Result<GeneratedPdf>.Success(new GeneratedPdf(pdf, downloadName));
    }

    /// <summary>
    /// The signed contract on file, if there is one.
    ///
    /// The newest upload wins — a contract re-signed after an amendment is uploaded again, and the
    /// later one is the one in force. Named after the candidate like everything else they
    /// download, but keeping the uploaded file's own extension: these come back from the parties
    /// as scans as often as PDFs.
    ///
    /// A row whose file has gone missing falls through to drawing one. Better our version than an
    /// error on a document the desk needs now.
    /// </summary>
    private async Task<GeneratedPdf?> SignedContractAsync(Candidate candidate, CancellationToken ct)
    {
        var uploads = await _tenant.CandidateDocuments
            .AsNoTracking()
            .Where(d => d.CandidateId == candidate.Id
                        && d.DocumentType == DocumentType.Contract
                        && !d.IsGenerated)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(ct);

        foreach (var upload in uploads)
        {
            var bytes = await ReadAsync(upload.FilePath, ct);
            if (bytes is null) continue;

            var extension = Path.GetExtension(upload.OriginalFileName);
            if (string.IsNullOrWhiteSpace(extension)) extension = Path.GetExtension(upload.FileName);

            return new GeneratedPdf(
                bytes,
                DocumentFileName.For(candidate.FullName, "Contract", extension.TrimStart('.') is { Length: > 0 } e ? e : "pdf"),
                string.IsNullOrWhiteSpace(upload.ContentType) ? "application/pdf" : upload.ContentType);
        }

        return null;
    }

    private async Task<byte[]?> ReadAsync(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            await using var stream = await _fileStorage.DownloadAsync(path, ct);
            if (stream is null) return null;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
