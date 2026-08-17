using FluentValidation;
using SimbaFlow.API.Features.Accounting.Commands;

namespace SimbaFlow.API.Features.Accounting.Validators;

public class UpsertExchangeRateValidator : AbstractValidator<UpsertExchangeRateCommand>
{
    public UpsertExchangeRateValidator()
    {
        RuleFor(x => x.FromCurrency).NotEmpty().Length(3);
        RuleFor(x => x.ToCurrency).NotEmpty().Length(3);
        RuleFor(x => x.Rate).GreaterThan(0);
        RuleFor(x => x.EffectiveDate).NotEmpty();
    }
}
