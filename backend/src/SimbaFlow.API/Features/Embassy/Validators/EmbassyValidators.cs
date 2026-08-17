using FluentValidation;
using SimbaFlow.API.Features.Embassy.Commands;

namespace SimbaFlow.API.Features.Embassy.Validators;

public class BookMedicalValidator : AbstractValidator<BookMedicalCommand>
{
    public BookMedicalValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.AppointmentDate).NotEmpty();
        RuleFor(x => x.FacilityName).NotEmpty().MaximumLength(200);
    }
}

public class RecordMedicalResultValidator : AbstractValidator<RecordMedicalResultCommand>
{
    public RecordMedicalResultValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Result).NotEmpty()
            .Must(r => r.Equals("Fit", StringComparison.OrdinalIgnoreCase)
                       || r.Equals("Unfit", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Result must be Fit or Unfit");
    }
}

public class BookTasheerValidator : AbstractValidator<BookTasheerCommand>
{
    public BookTasheerValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.AppointmentDate).NotEmpty();
    }
}

public class RecordTasheerResultValidator : AbstractValidator<RecordTasheerResultCommand>
{
    public RecordTasheerResultValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Result).NotEmpty()
            .Must(r => r.Equals("Book Done", StringComparison.OrdinalIgnoreCase)
                       || r.Equals("Expired", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Result must be 'Book Done' or 'Expired'");
    }
}

public class SetVisaReadyValidator : AbstractValidator<SetVisaReadyCommand>
{
    public SetVisaReadyValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
    }
}

public class SubmitVisaDocumentationValidator : AbstractValidator<SubmitVisaDocumentationCommand>
{
    public SubmitVisaDocumentationValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.ReferenceNumber).MaximumLength(100).When(x => x.ReferenceNumber is not null);
    }
}

public class RecordVisaOutcomeValidator : AbstractValidator<RecordVisaOutcomeCommand>
{
    public RecordVisaOutcomeValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty()
            .Must(o => o.Equals("Issued", StringComparison.OrdinalIgnoreCase)
                       || o.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Outcome must be Issued or Rejected");
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .MaximumLength(1000)
            .When(x => x.Outcome.Equals("Rejected", StringComparison.OrdinalIgnoreCase));
    }
}

public class ResubmitVisaValidator : AbstractValidator<ResubmitVisaCommand>
{
    public ResubmitVisaValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
    }
}
