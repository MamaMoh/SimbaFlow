using Carter;
using MediatR;
using SimbaFlow.API.Features.Staff.Commands;
using SimbaFlow.API.Features.Staff.Queries;

namespace SimbaFlow.API.Features.Staff;

public class StaffModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staff")
            .WithTags("Staff")
            .RequireAuthorization();

        // List staff profiles with filters
        group.MapGet("/", async (
            string? search,
            string? employmentStatus,
            string? staffType,
            Guid? departmentId,
            bool? hasAccount,
            ISender sender) =>
        {
            var query = new GetStaffProfilesQuery(search, employmentStatus, staffType, departmentId, hasAccount);
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get stats for dashboard cards
        group.MapGet("/stats", async (ISender sender) =>
        {
            var result = await sender.Send(new GetStaffStatsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get staff profile detail by ID
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetStaffProfileByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // HR drafts a new staff profile (no IT account yet)
        group.MapPost("/", async (DraftStaffProfileCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/staff/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // IT provisions a user account linked to an existing staff profile
        group.MapPost("/{staffProfileId:guid}/provision-account",
            async (Guid staffProfileId, ProvisionUserAccountRequest request, ISender sender) =>
            {
                var command = new ProvisionUserAccountCommand(
                    staffProfileId,
                    request.Username,
                    request.Email,
                    request.TemporaryPassword,
                    request.DepartmentId,
                    request.RequireMfa,
                    request.AllowedIpRanges,
                    request.RoleNames);

                var result = await sender.Send(command);
                return result.IsSuccess
                    ? Results.Created($"/api/users/{result.Data}", result)
                    : Results.Json(result, statusCode: result.StatusCode);
            });

        // Medical Administration assigns clinical privileges (department affiliation)
        group.MapPost("/{staffProfileId:guid}/privileges",
            async (Guid staffProfileId, AssignClinicalPrivilegesRequest request, ISender sender) =>
            {
                var command = new AssignClinicalPrivilegesCommand(
                    staffProfileId,
                    request.DepartmentId,
                    request.ClinicalRole,
                    request.EffectiveStartDate,
                    request.EffectiveEndDate,
                    request.IsPrimary,
                    request.Notes);

                var result = await sender.Send(command);
                return result.IsSuccess
                    ? Results.Created($"/api/staff/{staffProfileId}/privileges/{result.Data}", result)
                    : Results.Json(result, statusCode: result.StatusCode);
            });

        // Suspend staff (reversible block)
        group.MapPost("/{staffProfileId:guid}/suspend",
            async (Guid staffProfileId, SuspendStaffRequest request, ISender sender) =>
            {
                var command = new SuspendStaffCommand(staffProfileId, request.Reason);
                var result = await sender.Send(command);
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: result.StatusCode);
            });

        // Terminate staff (non-reversible)
        group.MapPost("/{staffProfileId:guid}/terminate",
            async (Guid staffProfileId, TerminateStaffRequest request, ISender sender) =>
            {
                var command = new TerminateStaffCommand(staffProfileId, request.Reason, request.TerminationDate);
                var result = await sender.Send(command);
                return result.IsSuccess
                    ? Results.Ok(result)
                    : Results.Json(result, statusCode: result.StatusCode);
            });
    }
}

// --- Request DTOs (for binding from route + body) ---

public record ProvisionUserAccountRequest(
    string Username,
    string Email,
    string TemporaryPassword,
    Guid? DepartmentId,
    bool RequireMfa,
    string[]? AllowedIpRanges,
    List<string>? RoleNames);

public record AssignClinicalPrivilegesRequest(
    Guid DepartmentId,
    string ClinicalRole,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate,
    bool IsPrimary,
    string? Notes);

public record SuspendStaffRequest(string Reason);

public record TerminateStaffRequest(string Reason, DateOnly TerminationDate);
