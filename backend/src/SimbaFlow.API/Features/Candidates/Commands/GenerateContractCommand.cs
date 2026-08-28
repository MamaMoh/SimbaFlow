using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Commands;

public record GenerateContractCommand(Guid CandidateId)
    : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateContractHandler : IRequestHandler<GenerateContractCommand, Result<byte[]>>
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

    public async Task<Result<byte[]>> Handle(GenerateContractCommand request, CancellationToken ct)
    {
        var candidate = await _tenant.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null)
            return Result<byte[]>.Failure("Candidate not found", 404);

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
            return Result<byte[]>.Failure(
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
        await using var stream = new MemoryStream(pdf);
        var relativePath = await _fileStorage.UploadAsync(
            _tenantContext.SchemaName ?? "default",
            candidate.Id,
            $"contract_{candidate.PassportNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
            "application/pdf",
            stream,
            ct);

        _tenant.CandidateDocuments.Add(new CandidateDocument
        {
            CandidateId = candidate.Id,
            FileName = Path.GetFileName(relativePath),
            OriginalFileName = $"Contract_{candidate.FullName.Replace(' ', '_')}.pdf",
            ContentType = "application/pdf",
            FilePath = relativePath,
            DocumentType = DocumentType.Contract,
            FileSizeBytes = pdf.Length,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.UserName
        });
        await _tenant.SaveChangesAsync(ct);

        return Result<byte[]>.Success(pdf);
    }
}
