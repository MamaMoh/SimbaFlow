using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The enjaze forms for a set of candidates, as one printable document.
/// </summary>
public record GenerateBulkVisaFormsCommand(List<Guid> CandidateIds)
    : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GenerateBulkVisaFormsHandler
    : IRequestHandler<GenerateBulkVisaFormsCommand, Result<byte[]>>
{
    private readonly ITenantDbContext _context;
    private readonly ICvGenerationService _cvGeneration;
    private readonly IFileStorageService _fileStorage;

    public GenerateBulkVisaFormsHandler(
        ITenantDbContext context, ICvGenerationService cvGeneration, IFileStorageService fileStorage)
    {
        _context = context;
        _cvGeneration = cvGeneration;
        _fileStorage = fileStorage;
    }

    public async Task<Result<byte[]>> Handle(GenerateBulkVisaFormsCommand request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? [])
            .Where(id => id != Guid.Empty).Distinct().Take(50).ToList();

        if (ids.Count == 0) return Result<byte[]>.Failure("Select at least one candidate", 400);

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync(ct);

        if (candidates.Count == 0) return Result<byte[]>.Failure("No candidates found", 404);

        var entries = new List<(Candidate, byte[]?)>(candidates.Count);
        foreach (var candidate in candidates)
        {
            byte[]? photo = null;
            if (!string.IsNullOrWhiteSpace(candidate.PhotoPath))
            {
                await using var stream = await _fileStorage.DownloadAsync(candidate.PhotoPath, ct);
                if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, ct);
                    photo = ms.ToArray();
                }
            }
            entries.Add((candidate, photo));
        }

        return Result<byte[]>.Success(await _cvGeneration.GenerateVisaFormsAsync(entries, ct));
    }
}
