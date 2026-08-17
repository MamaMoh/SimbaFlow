using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Queries;

public record GetCandidateMediaQuery(Guid CandidateId, string Kind)
    : IRequest<Result<CandidateMediaDto>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record CandidateMediaDto(byte[] Bytes, string ContentType, string FileName);

public class GetCandidateMediaHandler : IRequestHandler<GetCandidateMediaQuery, Result<CandidateMediaDto>>
{
    private readonly ITenantDbContext _context;
    private readonly IFileStorageService _fileStorage;

    public GetCandidateMediaHandler(ITenantDbContext context, IFileStorageService fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
    }

    public async Task<Result<CandidateMediaDto>> Handle(
        GetCandidateMediaQuery request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return Result<CandidateMediaDto>.Failure("Candidate not found", 404);

        string? path = null;
        string fileName = "file";
        var kind = request.Kind.Trim().ToLowerInvariant();

        switch (kind)
        {
            case "photo":
                path = candidate.PhotoPath;
                fileName = "photo.jpg";
                break;
            case "full-photo":
            case "fullphoto":
                path = candidate.FullPhotoPath;
                fileName = "full-photo.jpg";
                break;
            case "passport":
            {
                var doc = await _context.CandidateDocuments
                    .AsNoTracking()
                    .Where(d => d.CandidateId == request.CandidateId
                                && !d.IsDeleted
                                && d.DocumentType == DocumentType.Passport)
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                path = doc?.FilePath;
                fileName = doc?.OriginalFileName ?? "passport.jpg";
                break;
            }
            default:
                return Result<CandidateMediaDto>.Failure("Unknown media kind", 400);
        }

        if (string.IsNullOrWhiteSpace(path))
            return Result<CandidateMediaDto>.Failure("Media not found", 404);

        await using var stream = await _fileStorage.DownloadAsync(path, cancellationToken);
        if (stream is null)
            return Result<CandidateMediaDto>.Failure("Media file missing", 404);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        if (bytes.Length == 0)
            return Result<CandidateMediaDto>.Failure("Media file empty", 404);

        var contentType = GuessContentType(fileName, bytes);
        return Result<CandidateMediaDto>.Success(new CandidateMediaDto(bytes, contentType, fileName));
    }

    private static string GuessContentType(string fileName, byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
            return "image/png";
        if (bytes.Length >= 4 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46)
            return "image/webp";

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };
    }
}
