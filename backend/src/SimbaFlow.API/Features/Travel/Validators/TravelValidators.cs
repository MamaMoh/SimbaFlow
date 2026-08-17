using FluentValidation;
using SimbaFlow.API.Features.Travel.Commands;

namespace SimbaFlow.API.Features.Travel.Validators;

public class BookTicketValidator : AbstractValidator<BookTicketCommand>
{
    public BookTicketValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FlightDate).NotEmpty();
        RuleFor(x => x.TicketRef).MaximumLength(64).When(x => x.TicketRef is not null);
    }
}

public class MarkNotifiedValidator : AbstractValidator<MarkNotifiedCommand>
{
    public MarkNotifiedValidator() => RuleFor(x => x.CandidateId).NotEmpty();
}

public class ConfirmDepartedValidator : AbstractValidator<ConfirmDepartedCommand>
{
    public ConfirmDepartedValidator() => RuleFor(x => x.CandidateId).NotEmpty();
}

public class RecordNotDepartedValidator : AbstractValidator<RecordNotDepartedCommand>
{
    public RecordNotDepartedValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty()
            .Must(o => o.Equals("BackToTicket", StringComparison.OrdinalIgnoreCase)
                       || o.Equals("CancelDeparture", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Outcome must be BackToTicket or CancelDeparture");
        RuleFor(x => x.ReasonOther).NotEmpty()
            .When(x => x.Reason.Equals("Other", StringComparison.OrdinalIgnoreCase));
    }
}
