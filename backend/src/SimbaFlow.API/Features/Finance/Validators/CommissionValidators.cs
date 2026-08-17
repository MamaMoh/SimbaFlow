using FluentValidation;
using SimbaFlow.API.Features.Finance.Commands;

namespace SimbaFlow.API.Features.Finance.Validators;

public class UpsertCommissionFeesValidator : AbstractValidator<UpsertCommissionFeesCommand>
{
    public UpsertCommissionFeesValidator()
    {
        RuleFor(x => x.CommissionId).NotEmpty();
        RuleFor(x => x.Fees).NotNull();
        RuleForEach(x => x.Fees).ChildRules(fee =>
        {
            fee.RuleFor(f => f.FeeType).NotEmpty();
            fee.RuleFor(f => f.Amount).GreaterThanOrEqualTo(0);
        });
    }
}

public class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.CommissionId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Method).NotEmpty();
    }
}

public class OpenDisputeValidator : AbstractValidator<OpenDisputeCommand>
{
    public OpenDisputeValidator()
    {
        RuleFor(x => x.CommissionId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

public class ResolveDisputeValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeValidator()
    {
        RuleFor(x => x.DisputeId).NotEmpty();
        RuleFor(x => x.ResolutionNotes).NotEmpty().MaximumLength(4000);
    }
}
