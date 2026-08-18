using FluentValidation;
using SimbaFlow.API.Features.Candidates.Commands;

namespace SimbaFlow.API.Features.Candidates.Validators;

public class RegisterCandidateValidator : AbstractValidator<RegisterCandidateCommand>
{
    public RegisterCandidateValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MiddleName).MaximumLength(100).When(x => x.MiddleName is not null);
        RuleFor(x => x.PassportNumber)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^[A-Za-z0-9]+$")
            .WithMessage("Passport number must be alphanumeric");
        RuleFor(x => x.DateOfBirth)
            .NotEmpty()
            .Must(BeValidDate)
            .WithMessage("DateOfBirth must be a valid date (yyyy-MM-dd)")
            .Must(BeInPast)
            .WithMessage("DateOfBirth must be in the past");
        RuleFor(x => x.Gender).InclusiveBetween(0, 2);
        RuleFor(x => x.Nationality).MaximumLength(100).When(x => x.Nationality is not null);
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => x.PhoneNumber is not null);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500).When(x => x.Address is not null);
        RuleFor(x => x.City).MaximumLength(100).When(x => x.City is not null);
        RuleFor(x => x.Country).MaximumLength(100).When(x => x.Country is not null);
        RuleFor(x => x.LabourId).MaximumLength(50).When(x => x.LabourId is not null);
        RuleFor(x => x.CountryOfTravel).MaximumLength(100).When(x => x.CountryOfTravel is not null);
        RuleFor(x => x.PartnerName).MaximumLength(200).When(x => x.PartnerName is not null);
        RuleFor(x => x.ContractDate)
            .Must(BeValidDateOrEmpty)
            .WithMessage("ContractDate must be a valid date (yyyy-MM-dd)")
            .When(x => !string.IsNullOrWhiteSpace(x.ContractDate));
        // OfficeId may be empty — handler resolves from the current user's location.
        When(x => x.Intake is not null, () =>
        {
            RuleFor(x => x.Intake!.VisaNumber).MaximumLength(64).When(x => x.Intake!.VisaNumber is not null);
            RuleFor(x => x.Intake!.VisaType).MaximumLength(64).When(x => x.Intake!.VisaType is not null);
            RuleFor(x => x.Intake!.SponsorName).MaximumLength(256).When(x => x.Intake!.SponsorName is not null);
            RuleFor(x => x.Intake!.SponsorIdNumber).MaximumLength(64).When(x => x.Intake!.SponsorIdNumber is not null);
            RuleFor(x => x.Intake!.SponsorPhone).MaximumLength(32).When(x => x.Intake!.SponsorPhone is not null);
            RuleFor(x => x.Intake!.SponsorAddress).MaximumLength(512).When(x => x.Intake!.SponsorAddress is not null);
            RuleFor(x => x.Intake!.SponsorArabicName).MaximumLength(256).When(x => x.Intake!.SponsorArabicName is not null);
            RuleFor(x => x.Intake!.AgentName).MaximumLength(256).When(x => x.Intake!.AgentName is not null);
            RuleFor(x => x.Intake!.PassportIssueDate)
                .Must(BeValidDateOrEmpty)
                .When(x => !string.IsNullOrWhiteSpace(x.Intake!.PassportIssueDate));
            RuleFor(x => x.Intake!.PassportExpiryDate)
                .Must(BeValidDateOrEmpty)
                .When(x => !string.IsNullOrWhiteSpace(x.Intake!.PassportExpiryDate));
        });
    }

    private static bool BeValidDate(string value) =>
        DateOnly.TryParse(value, out _);

    private static bool BeValidDateOrEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, out _);

    private static bool BeInPast(string value) =>
        DateOnly.TryParse(value, out var d) && d < DateOnly.FromDateTime(DateTime.UtcNow);
}
