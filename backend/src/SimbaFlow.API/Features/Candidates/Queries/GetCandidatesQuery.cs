using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Candidates.Queries;

public record GetCandidatesQuery(
    int Page, int PageSize, string? Search,
    Guid? StageId, string? CountryOfTravel) : IRequest<Result<PaginatedCandidateResult>>, IRequirePermission
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
    string? PartnerName,
    string Status,
    DateTime RegisteredAt,
    string DateOfBirth,
    int? Age,
    string? Occupation,
    string? SponsorName,
    string? SponsorIdNumber,
    string? VisaNumber,
    string? AgentName,
    string? WorksIn);

public class GetCandidatesHandler : IRequestHandler<GetCandidatesQuery, Result<PaginatedCandidateResult>>
{
    private readonly ITenantDbContext _context;

    public GetCandidatesHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedCandidateResult>> Handle(GetCandidatesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
                (c.LabourId != null && EF.Functions.ILike(c.LabourId, $"%{search}%")) ||
                (c.SponsorName != null && EF.Functions.ILike(c.SponsorName, $"%{search}%")) ||
                (c.VisaNumber != null && EF.Functions.ILike(c.VisaNumber, $"%{search}%")));
        }

        if (request.StageId.HasValue)
            query = query.Where(c => c.CurrentStageId == request.StageId);

        if (!string.IsNullOrWhiteSpace(request.CountryOfTravel))
            query = query.Where(c => c.CountryOfTravel == request.CountryOfTravel);

        var totalCount = await query.CountAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await query
            .OrderByDescending(c => c.RegisteredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CandidateListDto(
                c.Id,
                string.IsNullOrEmpty(c.MiddleName)
                    ? c.FirstName + " " + c.LastName
                    : c.FirstName + " " + c.MiddleName + " " + c.LastName,
                c.PassportNumber,
                c.LabourId,
                c.CurrentStageName,
                c.CountryOfTravel,
                c.PartnerName,
                c.Status.ToString(),
                c.RegisteredAt,
                c.DateOfBirth.ToString("yyyy-MM-dd"),
                today.Year - c.DateOfBirth.Year -
                    ((c.DateOfBirth.Month > today.Month ||
                      (c.DateOfBirth.Month == today.Month && c.DateOfBirth.Day > today.Day)) ? 1 : 0),
                c.Occupation,
                c.SponsorName,
                c.SponsorIdNumber,
                c.VisaNumber,
                c.AgentName,
                c.WorksIn ?? c.CountryOfTravel))
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
    string? LocalFullName,
    string PassportNumber,
    string? LabourId,
    string? BiometricId,
    string? NationalId,
    string DateOfBirth,
    string? PlaceOfBirth,
    int Gender,
    string? Nationality,
    string? Religion,
    string? MaritalStatus,
    int? NumberOfChildren,
    string? Height,
    string? Weight,
    string? PassportType,
    string? PassportPlaceOfIssue,
    string? PassportIssueDate,
    string? PassportExpiryDate,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? Region,
    string? Subcity,
    string? Woreda,
    string? HouseNo,
    string? Occupation,
    string? Qualification,
    string? MonthlySalary,
    string? ContractPeriod,
    string? EnglishLevel,
    string? ArabicLevel,
    string? OtherLanguages,
    int? ExperienceAbroadYears,
    string? WorksIn,
    string? ReferenceNo,
    string? Remark,
    string? CookingLevel,
    bool SkillCleaning,
    bool SkillWashing,
    bool SkillCooking,
    bool SkillIroning,
    bool SkillSewing,
    bool SkillBabysitting,
    bool SkillChildCare,
    string? CountryOfTravel,
    string? PartnerName,
    Guid? PartnerAgencyId,
    string? ContractDate,
    string? PhotoPath,
    string? FullPhotoPath,
    string? VisaNumber,
    string? VisaType,
    string? SponsorName,
    string? SponsorIdNumber,
    string? SponsorPhone,
    string? SponsorAddress,
    string? SponsorArabicName,
    string? AgentName,
    string? ApplicationNo,
    string? FileNo,
    string? WakalaNo,
    string? ContractNo,
    string? StickerVisaNo,
    string? SignedOn,
    string? RelativeName,
    string? RelativePhone,
    string? RelativeKinship,
    string? RelativeGender,
    string? RelativeBirthDate,
    string? RelativeCity,
    string? RelativeRegion,
    string? RelativeSubcity,
    string? RelativeWoreda,
    string? RelativeHouseNo,
    string? ContactPerson2,
    string? ContactPhone2,
    string? CocCenterName,
    string? CertificateNo,
    string? CertifiedDate,
    string? MedicalPlace,
    string? CurrentStageName,
    Guid? CurrentStageId,
    DateTime RegisteredAt,
    string? RegisteredBy);

public class GetCandidateByIdHandler : IRequestHandler<GetCandidateByIdQuery, Result<CandidateDetailDto>>
{
    private readonly ITenantDbContext _context;

    public GetCandidateByIdHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CandidateDetailDto>> Handle(GetCandidateByIdQuery request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .AsNoTracking()
            .Where(c => c.Id == request.Id && !c.IsDeleted)
            .Select(c => new CandidateDetailDto(
                c.Id, c.FirstName, c.LastName, c.MiddleName, c.LocalFullName,
                c.PassportNumber, c.LabourId, c.BiometricId, c.NationalId,
                c.DateOfBirth.ToString("yyyy-MM-dd"), c.PlaceOfBirth, (int)c.Gender,
                c.Nationality, c.Religion, c.MaritalStatus, c.NumberOfChildren,
                c.Height, c.Weight,
                c.PassportType, c.PassportPlaceOfIssue,
                c.PassportIssueDate.HasValue ? c.PassportIssueDate.Value.ToString("yyyy-MM-dd") : null,
                c.PassportExpiryDate.HasValue ? c.PassportExpiryDate.Value.ToString("yyyy-MM-dd") : null,
                c.PhoneNumber, c.Email,
                c.Address, c.City, c.Country, c.Region, c.Subcity, c.Woreda, c.HouseNo,
                c.Occupation, c.Qualification, c.MonthlySalary, c.ContractPeriod,
                c.EnglishLevel, c.ArabicLevel, c.OtherLanguages, c.ExperienceAbroadYears, c.WorksIn,
                c.ReferenceNo, c.Remark, c.CookingLevel,
                c.SkillCleaning, c.SkillWashing, c.SkillCooking, c.SkillIroning,
                c.SkillSewing, c.SkillBabysitting, c.SkillChildCare,
                c.CountryOfTravel, c.PartnerName, c.PartnerAgencyId,
                c.ContractDate.HasValue ? c.ContractDate.Value.ToString("yyyy-MM-dd") : null,
                c.PhotoPath, c.FullPhotoPath,
                c.VisaNumber, c.VisaType, c.SponsorName, c.SponsorIdNumber,
                c.SponsorPhone, c.SponsorAddress, c.SponsorArabicName, c.AgentName,
                c.ApplicationNo, c.FileNo, c.WakalaNo, c.ContractNo, c.StickerVisaNo,
                c.SignedOn.HasValue ? c.SignedOn.Value.ToString("yyyy-MM-dd") : null,
                c.RelativeName, c.RelativePhone, c.RelativeKinship, c.RelativeGender,
                c.RelativeBirthDate.HasValue ? c.RelativeBirthDate.Value.ToString("yyyy-MM-dd") : null,
                c.RelativeCity, c.RelativeRegion, c.RelativeSubcity, c.RelativeWoreda, c.RelativeHouseNo,
                c.ContactPerson2, c.ContactPhone2, c.CocCenterName, c.CertificateNo,
                c.CertifiedDate.HasValue ? c.CertifiedDate.Value.ToString("yyyy-MM-dd") : null,
                c.MedicalPlace,
                c.CurrentStageName, c.CurrentStageId,
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
    private readonly ITenantDbContext _context;

    public GetCandidateDocumentsHandler(ITenantDbContext context) => _context = context;

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
    private readonly ITenantDbContext _context;

    public GetCandidateTimelineHandler(ITenantDbContext context) => _context = context;

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
