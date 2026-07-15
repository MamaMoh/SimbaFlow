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
        group.MapPost("/login", async (LoginCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/refresh", async (RefreshTokenCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Protected endpoints (auth required)
        group.MapPost("/logout", async (LogoutCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();

        group.MapPost("/change-password", async (ChangePasswordCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).RequireAuthorization();

        // MFA verification (second step of login)
        group.MapPost("/login/mfa", async (VerifyMfaCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Ok(result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // MFA setup (authenticated user enables MFA)
        group.MapPost("/mfa/setup", async (ISender sender) =>
        {
            var result = await sender.Send(new SetupMfaCommand());
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
