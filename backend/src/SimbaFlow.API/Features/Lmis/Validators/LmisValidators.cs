using FluentValidation;
using SimbaFlow.API.Features.Lmis.Commands;

namespace SimbaFlow.API.Features.Lmis.Validators;

public class RecordInsurancePaidValidator : AbstractValidator<RecordInsurancePaidCommand>
{
    public RecordInsurancePaidValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
    }
}

public class AdvanceLmisMilestoneValidator : AbstractValidator<AdvanceLmisMilestoneCommand>
{
    public AdvanceLmisMilestoneValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Milestone).NotEmpty()
            .Must(m => m.Equals("Uploaded", StringComparison.OrdinalIgnoreCase)
                       || m.Equals("Check Verified", StringComparison.OrdinalIgnoreCase)
                       || m.Equals("Issued", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Milestone must be Uploaded, Check Verified, or Issued");
    }
}
