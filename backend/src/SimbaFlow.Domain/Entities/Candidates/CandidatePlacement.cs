using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// 1:1 sponsor, visa, and contract placement details for a candidate.
/// </summary>
public class CandidatePlacement : BaseEntity
{
    public Guid CandidateId { get; set; }

    public string? CountryOfTravel { get; set; }
    public string? WorksIn { get; set; }
    public Guid? PartnerId { get; set; }

    public string? VisaNumber { get; set; }
    public string? VisaType { get; set; }
    public string? StickerVisaNumber { get; set; }

    public string? SponsorId { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorNameArabic { get; set; }
    public string? SponsorPhone { get; set; }
    public string? SponsorAddress { get; set; }
    public string? SponsorEmail { get; set; }

    public string? Agent { get; set; }
    public string? NationalId { get; set; }

    public string? ContractNumber { get; set; }
    public string? WakalaNumber { get; set; }
    public DateOnly? SignedOn { get; set; }
    public DateOnly? ContractDate { get; set; }

    public string? CocCenter { get; set; }
    public DateOnly? CertifiedDate { get; set; }
    public string? CertificateNumber { get; set; }
    public string? TrainingType { get; set; }

    public decimal? Salary { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }

    public Candidate? Candidate { get; set; }
}
