using FluentValidation;
using SimbaFlow.API.Features.Candidates.Commands;

namespace SimbaFlow.API.Features.Candidates.Validators;

public class UpdateCandidateValidator : AbstractValidator<UpdateCandidateCommand>
{
    public UpdateCandidateValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100).When(x => x.MiddleName is not null);
        RuleFor(x => x.Nationality).MaximumLength(100).When(x => x.Nationality is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500).When(x => x.Address is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.Country).MaximumLength(100).When(x => x.Country is not null);
        RuleFor(x => x.LabourId).MaximumLength(50).When(x => x.LabourId is not null);
        RuleFor(x => x.CountryOfTravel).MaximumLength(100).When(x => x.CountryOfTravel is not null);
        RuleFor(x => x.OfficeName).MaximumLength(200).When(x => x.OfficeName is not null);
        RuleFor(x => x.ContractDate)
            .Must(BeValidDateOrEmpty)
            .WithMessage("ContractDate must be a valid date (yyyy-MM-dd)")
            .When(x => !string.IsNullOrWhiteSpace(x.ContractDate));
    }

    private static bool BeValidDateOrEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, out _);
}
