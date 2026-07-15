using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Queries;

public record GetCandidatesQuery(
    int Page, int PageSize, string? Search,
    Guid? StageId, Guid? OfficeId, string? CountryOfTravel) : IRequest<Result<PaginatedCandidateResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record PaginatedCandidateResult(
    List<CandidateListDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CandidateListDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? CurrentStageName,
    string? CountryOfTravel,
    string? OfficeName,
    string Status,
    DateTime RegisteredAt);

public class GetCandidatesHandler : IRequestHandler<GetCandidatesQuery, Result<PaginatedCandidateResult>>
{
    private readonly IApplicationDbContext _context;

    public GetCandidatesHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedCandidateResult>> Handle(GetCandidatesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        // Text search
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
                (c.LabourId != null && EF.Functions.ILike(c.LabourId, $"%{search}%")));
        }

        // Filters
        if (request.StageId.HasValue)
            query = query.Where(c => c.CurrentStageId == request.StageId);

        if (request.OfficeId.HasValue)
            query = query.Where(c => c.OfficeId == request.OfficeId);

        if (!string.IsNullOrWhiteSpace(request.CountryOfTravel))
            query = query.Where(c => c.CountryOfTravel == request.CountryOfTravel);

        // Count
        var totalCount = await query.CountAsync(cancellationToken);

        // Paginate
        var items = await query
            .OrderByDescending(c => c.RegisteredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CandidateListDto(
                c.Id,
                c.FirstName + " " + c.LastName,
                c.PassportNumber,
                c.LabourId,
                c.CurrentStageName,
                c.CountryOfTravel,
                c.OfficeName,
                c.Status.ToString(),
                c.RegisteredAt))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return Result<PaginatedCandidateResult>.Success(
            new PaginatedCandidateResult(items, totalCount, request.Page, request.PageSize, totalPages));
    }
}

public record GetCandidateByIdQuery(Guid Id) : IRequest<Result<CandidateDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record CandidateDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string PassportNumber,
    string? LabourId,
    string DateOfBirth,
    int Gender,
    string? Nationality,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? CountryOfTravel,
    string? OfficeName,
    string? ContractDate,
    Guid OfficeId,
    string? CurrentStageName,
    Guid? CurrentStageId,
    DateTime RegisteredAt,
    string? RegisteredBy);

public class GetCandidateByIdHandler : IRequestHandler<GetCandidateByIdQuery, Result<CandidateDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCandidateByIdHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CandidateDetailDto>> Handle(GetCandidateByIdQuery request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .AsNoTracking()
            .Where(c => c.Id == request.Id && !c.IsDeleted)
            .Select(c => new CandidateDetailDto(
                c.Id, c.FirstName, c.LastName, c.MiddleName,
                c.PassportNumber, c.LabourId,
                c.DateOfBirth.ToString("yyyy-MM-dd"), (int)c.Gender,
                c.Nationality, c.PhoneNumber, c.Email,
                c.Address, c.City, c.Country,
                c.CountryOfTravel, c.OfficeName,
                c.ContractDate.HasValue ? c.ContractDate.Value.ToString("yyyy-MM-dd") : null,
                c.OfficeId, c.CurrentStageName, c.CurrentStageId,
                c.RegisteredAt, c.RegisteredBy))
            .FirstOrDefaultAsync(cancellationToken);

        return candidate is not null
            ? Result<CandidateDetailDto>.Success(candidate)
            : Result<CandidateDetailDto>.Failure("Candidate not found", 404);
    }
}

public record GetCandidateDocumentsQuery(Guid CandidateId) : IRequest<Result<List<CandidateDocumentDto>>>;

public record CandidateDocumentDto(Guid Id, string OriginalFileName, string ContentType, int DocumentType, long FileSizeBytes, DateTime UploadedAt);

public class GetCandidateDocumentsHandler : IRequestHandler<GetCandidateDocumentsQuery, Result<List<CandidateDocumentDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCandidateDocumentsHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<CandidateDocumentDto>>> Handle(GetCandidateDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _context.CandidateDocuments
            .AsNoTracking()
            .Where(d => d.CandidateId == request.CandidateId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new CandidateDocumentDto(d.Id, d.OriginalFileName, d.ContentType, (int)d.DocumentType, d.FileSizeBytes, d.UploadedAt))
            .ToListAsync(cancellationToken);

        return Result<List<CandidateDocumentDto>>.Success(docs);
    }
}

public record GetCandidateTimelineQuery(Guid CandidateId) : IRequest<Result<List<TimelineEntryDto>>>;

public record TimelineEntryDto(Guid Id, int EventType, string? FromStageName, string? ToStageName, string UserName, DateTime Timestamp, string? Notes);

public class GetCandidateTimelineHandler : IRequestHandler<GetCandidateTimelineQuery, Result<List<TimelineEntryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCandidateTimelineHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<TimelineEntryDto>>> Handle(GetCandidateTimelineQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.WorkflowEvents
            .AsNoTracking()
            .Where(e => e.CandidateId == request.CandidateId)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new TimelineEntryDto(e.Id, (int)e.EventType, e.FromStageName, e.ToStageName, e.UserName, e.Timestamp, e.Notes))
            .ToListAsync(cancellationToken);

        return Result<List<TimelineEntryDto>>.Success(events);
    }
}

public record GenerateCVCommand(Guid CandidateId) : IRequest<Result<byte[]>>;

public class GenerateCVHandler : IRequestHandler<GenerateCVCommand, Result<byte[]>>
{
    public Task<Result<byte[]>> Handle(GenerateCVCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement with QuestPDF in Unit 2 continued
        return Task.FromResult(Result<byte[]>.Failure("CV generation not yet implemented", 501));
    }
}
