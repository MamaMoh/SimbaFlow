using FluentValidation;
using SimbaFlow.API.Features.Exceptions.Commands;

namespace SimbaFlow.API.Features.Exceptions.Validators;

public class AddInvestigationNoteValidator : AbstractValidator<AddInvestigationNoteCommand>
{
    public AddInvestigationNoteValidator()
    {
        RuleFor(x => x.ExceptionCaseId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public class UpdateExceptionStatusValidator : AbstractValidator<UpdateExceptionStatusCommand>
{
    public UpdateExceptionStatusValidator()
    {
        RuleFor(x => x.ExceptionCaseId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty();
    }
}

public class AssignLiabilityValidator : AbstractValidator<AssignLiabilityCommand>
{
    public AssignLiabilityValidator()
    {
        RuleFor(x => x.ExceptionCaseId).NotEmpty();
        RuleFor(x => x.Party).NotEmpty();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(8);
    }
}

public class CloseExceptionValidator : AbstractValidator<CloseExceptionCommand>
{
    public CloseExceptionValidator()
    {
        RuleFor(x => x.ExceptionCaseId).NotEmpty();
        RuleFor(x => x.ResolutionSummary).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.FinancialImpactAmount).GreaterThanOrEqualTo(0)
            .When(x => x.FinancialImpactAmount.HasValue);
    }
}
