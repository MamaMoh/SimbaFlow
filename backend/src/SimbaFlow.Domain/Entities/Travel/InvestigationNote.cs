using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Travel;

public class InvestigationNote : BaseEntity
{
    public Guid ExceptionCaseId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional refs to CandidateDocument ids.</summary>
    public Guid[] AttachmentDocumentIds { get; set; } = [];

    public ExceptionCase? ExceptionCase { get; set; }
}
