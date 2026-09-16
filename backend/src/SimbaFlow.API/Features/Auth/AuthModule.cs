using Carter;
using MediatR;
using SimbaFlow.API.Features.Auth.Commands;
using SimbaFlow.API.Features.Auth.Queries;

namespace SimbaFlow.API.Features.Auth;

public class AuthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // Public endpoints (no auth required)
        // Rate limited per caller. Identity lockout already slows repeated attempts against one
        // account; this is what stops the same password being tried against many accounts, which
        // lockout does nothing about.
        group.MapPost("/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireRateLimiting("login");

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireRateLimiting("refresh");

        // Protected endpoints (auth required)
        group.MapPost("/logout", async (LogoutCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();

        // Anonymous by necessity — the caller has no session. Both are rate-limited: this is the
        // one pair of endpoints that accepts an arbitrary email and does work with it.
        group.MapPost("/forgot-password", async (ForgotPasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/reset-password", async (ResetPasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        }).AllowAnonymous().RequireRateLimiting("auth");

        group.MapPost("/change-password", async (ChangePasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();

        // MFA verification (second step of login)
        // A six-digit code is guessable in a way a password is not, so the second factor needs the
        // limit at least as much as the first.
        group.MapPost("/login/mfa", async (VerifyMfaCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireRateLimiting("login");

        // MFA setup (authenticated user requests a new authenticator key + QR)
        group.MapPost("/mfa/setup", async (ISender sender) =>
        {
            var result = await sender.Send(new SetupMfaCommand());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization("MfaEnrollment");

        // MFA enable (confirm enrollment with the first TOTP code → turns MFA on)
        group.MapPost("/mfa/enable", async (EnableMfaCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization("MfaEnrollment");

        // MFA disable (requires account password)
        group.MapPost("/mfa/disable", async (DisableMfaCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();

        // Current user profile
        group.MapGet("/me", async (ISender sender) =>
        {
            var result = await sender.Send(new Queries.GetMyProfileQuery());
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();
    }
}
