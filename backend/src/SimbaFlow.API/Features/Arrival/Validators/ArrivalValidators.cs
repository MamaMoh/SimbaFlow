using FluentValidation;
using SimbaFlow.API.Features.Arrival.Commands;

namespace SimbaFlow.API.Features.Arrival.Validators;

public class ConfirmArrivedValidator : AbstractValidator<ConfirmArrivedCommand>
{
    public ConfirmArrivedValidator() => RuleFor(x => x.CandidateId).NotEmpty();
}

public class FlagExceptionValidator : AbstractValidator<FlagExceptionCommand>
{
    public FlagExceptionValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Type).NotEmpty()
            .Must(t => t.Equals("Returned", StringComparison.OrdinalIgnoreCase)
                       || t.Equals("Runaway", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Type must be Returned or Runaway");
    }
}

public class AddToCommissionValidator : AbstractValidator<AddToCommissionCommand>
{
    public AddToCommissionValidator() => RuleFor(x => x.CandidateId).NotEmpty();
}
