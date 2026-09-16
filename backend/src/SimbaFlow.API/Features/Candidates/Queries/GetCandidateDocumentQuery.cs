using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Candidates.Queries;

/// <summary>
/// Downloads one of a candidate's uploaded documents.
///
/// Exists as a query so that the request passes through AuthorizationBehavior. Written inline in the
/// module, as it was, it checked only that the caller was signed in — so any user in the agency
/// could pull any candidate's passport scan given two ids, which the listing endpoint hands out.
/// Schema isolation still held the tenant boundary; the role boundary was not held at all.
/// </summary>
public record GetCandidateDocumentQuery(Guid CandidateId, Guid DocumentId)
    : IRequest<Result<CandidateDocumentFileDto>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record CandidateDocumentFileDto(Stream Content, string ContentType, string FileName);

public class GetCandidateDocumentHandler
    : IRequestHandler<GetCandidateDocumentQuery, Result<CandidateDocumentFileDto>>
{
    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _storage;

    public GetCandidateDocumentHandler(ITenantDbContext context, IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<Result<CandidateDocumentFileDto>> Handle(
        GetCandidateDocumentQuery request, CancellationToken cancellationToken)
    {
        var doc = await _context.CandidateDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.Id == request.DocumentId
                     && d.CandidateId == request.CandidateId
                     && !d.IsDeleted,
                cancellationToken);

        if (doc is null)
            return Result<CandidateDocumentFileDto>.Failure("Document not found", 404);

        var stream = await _storage.DownloadAsync(doc.FilePath, cancellationToken);
        if (stream is null)
            return Result<CandidateDocumentFileDto>.Failure("File is missing from storage", 404);

        return Result<CandidateDocumentFileDto>.Success(
            new CandidateDocumentFileDto(stream, doc.ContentType, doc.OriginalFileName));
    }
}
