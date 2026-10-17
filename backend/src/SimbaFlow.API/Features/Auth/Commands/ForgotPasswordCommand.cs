using System.Text;
using System.Web;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Email;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.API.Features.Auth.Commands;

/// <summary>Accepts a username or an email address — people forget which they signed up with.</summary>
public record ForgotPasswordCommand(string Email) : IRequest<Result>;

public class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Enter your email address or username");
    }
}

public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _email;
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<ForgotPasswordHandler> _logger;

    public ForgotPasswordHandler(
        UserManager<ApplicationUser> userManager,
        IEmailService email,
        IOptionsMonitor<EmailOptions> options,
        ILogger<ForgotPasswordHandler> logger)
    {
        _userManager = userManager;
        _email = email;
        _options = options;
        _logger = logger;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var identifier = request.Email.Trim();

        var user = await _userManager.FindByEmailAsync(identifier)
                   ?? await _userManager.FindByNameAsync(identifier);

        // Always answer the same way. Telling the caller whether an account exists turns this
        // endpoint into a way to enumerate every user of every agency.
        if (user is null || user.IsDeleted)
        {
            _logger.LogInformation("Password reset requested for an unknown account");
            return Result.Success();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var baseUrl = _options.CurrentValue.AppBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/reset-password?email={HttpUtility.UrlEncode(user.Email)}&token={encoded}";

        var body = EmailLayout.Render(new EmailContent(
            Kicker: "Account security",
            Title: "Reset your password",
            Greeting: EmailLayout.Greeting(user.FirstName),
            BodyHtml: EmailLayout.Paragraph(
                "We received a request to reset the password for your SimbaFlow account. "
                + "Choose a new one with the button below."),
            Button: new EmailButton("Choose a new password", link),
            NoteHtml: "<b style=\"color:#2b3631;\">Didn't ask for this?</b> Ignore this email — your "
                + "password won't change. The link expires in a few hours and works once."));

        await _email.SendAsync(user.Email!, "Reset your SimbaFlow password", body, ct);

        // Still Success even if the mail host was down: the caller must not learn the difference.
        return Result.Success();
    }
}
