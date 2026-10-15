using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Reports.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Reports.Schedules;

public record ReportScheduleDto(
    Guid Id,
    string ReportKey,
    string ReportName,
    string Format,
    string Frequency,
    int HourLocal,
    int? DayOfWeek,
    int? DayOfMonth,
    List<string> Recipients,
    bool IsActive,
    string Description,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastStatus,
    string? LastError,
    string? CreatedBy,
    DateTime CreatedAt)
{
    public static ReportScheduleDto From(ReportSchedule s) => new(
        s.Id,
        s.ReportKey,
        ReportCatalog.Items.FirstOrDefault(i => i.Key == s.ReportKey)?.Name ?? s.ReportKey,
        s.Format,
        s.Frequency.ToString().ToLowerInvariant(),
        s.HourLocal,
        s.DayOfWeek is null ? null : (int)s.DayOfWeek,
        s.DayOfMonth,
        ReportScheduleRules.ParseRecipients(s.Recipients).Valid,
        s.IsActive,
        ReportScheduleRules.Describe(s.Frequency, s.HourLocal, s.DayOfWeek, s.DayOfMonth),
        s.NextRunAt,
        s.LastRunAt,
        s.LastStatus,
        s.LastError,
        s.CreatedBy,
        s.CreatedAt);
}

/// <summary>Fields shared by create and update.</summary>
public interface IReportScheduleFields
{
    string ReportKey { get; }
    string Format { get; }
    string Frequency { get; }
    int HourLocal { get; }
    int? DayOfWeek { get; }
    int? DayOfMonth { get; }
    string Recipients { get; }
}

internal static class ReportScheduleFieldRules
{
    public static ReportScheduleFrequency? ParseFrequency(string? value) =>
        Enum.TryParse<ReportScheduleFrequency>(value, ignoreCase: true, out var f) && Enum.IsDefined(f)
            ? f
            : null;

    public static void Apply<T>(AbstractValidator<T> v) where T : IReportScheduleFields
    {
        v.RuleFor(x => x.ReportKey)
            .Must(ReportCatalog.Exists).WithMessage("Pick a report from the list.");
        v.RuleFor(x => x.Format)
            .Must(f => ReportScheduleRules.Formats.Contains((f ?? "").ToLowerInvariant()))
            .WithMessage("Format must be 'excel' or 'pdf'.");
        v.RuleFor(x => x.Frequency)
            .Must(f => ParseFrequency(f) is not null)
            .WithMessage("Frequency must be daily, weekly or monthly.");
        v.RuleFor(x => x.HourLocal).InclusiveBetween(0, 23);
        v.RuleFor(x => x.DayOfWeek)
            .NotNull().InclusiveBetween(0, 6)
            .When(x => ParseFrequency(x.Frequency) == ReportScheduleFrequency.Weekly)
            .WithMessage("Pick the day of the week it goes out.");
        v.RuleFor(x => x.DayOfMonth)
            .NotNull().InclusiveBetween(1, ReportScheduleRules.MaxDayOfMonth)
            .When(x => ParseFrequency(x.Frequency) == ReportScheduleFrequency.Monthly)
            .WithMessage($"Pick a day of the month between 1 and {ReportScheduleRules.MaxDayOfMonth}.");
        v.RuleFor(x => x.Recipients).Custom((raw, ctx) =>
        {
            var (valid, invalid) = ReportScheduleRules.ParseRecipients(raw);
            if (invalid.Count > 0)
                ctx.AddFailure("Recipients", $"Not an email address: {string.Join(", ", invalid)}");
            else if (valid.Count == 0)
                ctx.AddFailure("Recipients", "Add at least one email address.");
            else if (valid.Count > ReportScheduleRules.MaxRecipients)
                ctx.AddFailure("Recipients", $"A schedule can go to at most {ReportScheduleRules.MaxRecipients} addresses.");
        });
    }

    /// <summary>Copies the fields onto the entity and works out when it next goes out.</summary>
    public static void CopyTo(IReportScheduleFields f, ReportSchedule s, DateTime nowUtc)
    {
        var frequency = ParseFrequency(f.Frequency)!.Value;
        s.ReportKey = f.ReportKey;
        s.Format = f.Format.ToLowerInvariant();
        s.Frequency = frequency;
        s.HourLocal = f.HourLocal;
        s.DayOfWeek = frequency == ReportScheduleFrequency.Weekly ? (System.DayOfWeek)f.DayOfWeek!.Value : null;
        s.DayOfMonth = frequency == ReportScheduleFrequency.Monthly ? f.DayOfMonth : null;
        s.Recipients = string.Join(", ", ReportScheduleRules.ParseRecipients(f.Recipients).Valid);
        s.NextRunAt = ReportScheduleRules.NextRunUtc(s.Frequency, s.HourLocal, s.DayOfWeek, s.DayOfMonth, nowUtc);
    }
}

// ─── List ──────────────────────────────────────────────────────────────

public record GetReportSchedulesQuery : IRequest<Result<List<ReportScheduleDto>>>, IRequirePermission
{
    public string RequiredPermission => "report.schedule";
}

public class GetReportSchedulesHandler : IRequestHandler<GetReportSchedulesQuery, Result<List<ReportScheduleDto>>>
{
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _user;

    public GetReportSchedulesHandler(IPlatformDbContext platform, ICurrentUserService user)
    {
        _platform = platform;
        _user = user;
    }

    public async Task<Result<List<ReportScheduleDto>>> Handle(GetReportSchedulesQuery request, CancellationToken ct)
    {
        if (_user.TenantId is not Guid tenantId)
            return Result<List<ReportScheduleDto>>.Failure("Scheduled reports belong to an agency.", 400);

        var rows = await _platform.ReportSchedules.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return Result<List<ReportScheduleDto>>.Success(rows.Select(ReportScheduleDto.From).ToList());
    }
}

// ─── Create ────────────────────────────────────────────────────────────

public record CreateReportScheduleCommand(
    string ReportKey,
    string Format,
    string Frequency,
    int HourLocal,
    int? DayOfWeek,
    int? DayOfMonth,
    string Recipients,
    bool IsActive = true) : IRequest<Result<ReportScheduleDto>>, IRequirePermission, IReportScheduleFields
{
    public string RequiredPermission => "report.schedule";
}

public class CreateReportScheduleValidator : AbstractValidator<CreateReportScheduleCommand>
{
    public CreateReportScheduleValidator() => ReportScheduleFieldRules.Apply(this);
}

public class CreateReportScheduleHandler : IRequestHandler<CreateReportScheduleCommand, Result<ReportScheduleDto>>
{
    /// <summary>Enough for any agency; stops a loop in a script filling the table.</summary>
    public const int MaxSchedulesPerAgency = 50;

    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _user;

    public CreateReportScheduleHandler(IPlatformDbContext platform, ICurrentUserService user)
    {
        _platform = platform;
        _user = user;
    }

    public async Task<Result<ReportScheduleDto>> Handle(CreateReportScheduleCommand request, CancellationToken ct)
    {
        if (_user.TenantId is not Guid tenantId)
            return Result<ReportScheduleDto>.Failure("Scheduled reports belong to an agency.", 400);

        var count = await _platform.ReportSchedules.CountAsync(s => s.TenantId == tenantId && !s.IsDeleted, ct);
        if (count >= MaxSchedulesPerAgency)
            return Result<ReportScheduleDto>.Failure(
                $"An agency can have at most {MaxSchedulesPerAgency} scheduled reports.", 409);

        var schedule = new ReportSchedule
        {
            TenantId = tenantId,
            IsActive = request.IsActive,
            CreatedBy = _user.UserName,
        };
        ReportScheduleFieldRules.CopyTo(request, schedule, DateTime.UtcNow);

        _platform.ReportSchedules.Add(schedule);
        await _platform.SaveChangesAsync(ct);

        return Result<ReportScheduleDto>.Success(ReportScheduleDto.From(schedule), 201);
    }
}

// ─── Update ────────────────────────────────────────────────────────────

public record UpdateReportScheduleCommand(
    Guid Id,
    string ReportKey,
    string Format,
    string Frequency,
    int HourLocal,
    int? DayOfWeek,
    int? DayOfMonth,
    string Recipients,
    bool IsActive) : IRequest<Result<ReportScheduleDto>>, IRequirePermission, IReportScheduleFields
{
    public string RequiredPermission => "report.schedule";
}

public class UpdateReportScheduleValidator : AbstractValidator<UpdateReportScheduleCommand>
{
    public UpdateReportScheduleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        ReportScheduleFieldRules.Apply(this);
    }
}

public class UpdateReportScheduleHandler : IRequestHandler<UpdateReportScheduleCommand, Result<ReportScheduleDto>>
{
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _user;

    public UpdateReportScheduleHandler(IPlatformDbContext platform, ICurrentUserService user)
    {
        _platform = platform;
        _user = user;
    }

    public async Task<Result<ReportScheduleDto>> Handle(UpdateReportScheduleCommand request, CancellationToken ct)
    {
        var schedule = await ReportScheduleLookup.FindAsync(_platform, _user, request.Id, ct);
        if (schedule is null)
            return Result<ReportScheduleDto>.Failure("Scheduled report not found.", 404);

        ReportScheduleFieldRules.CopyTo(request, schedule, DateTime.UtcNow);
        schedule.IsActive = request.IsActive;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = _user.UserName;

        await _platform.SaveChangesAsync(ct);
        return Result<ReportScheduleDto>.Success(ReportScheduleDto.From(schedule));
    }
}

// ─── Delete ────────────────────────────────────────────────────────────

public record DeleteReportScheduleCommand(Guid Id) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "report.schedule";
}

public class DeleteReportScheduleHandler : IRequestHandler<DeleteReportScheduleCommand, Result>
{
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _user;

    public DeleteReportScheduleHandler(IPlatformDbContext platform, ICurrentUserService user)
    {
        _platform = platform;
        _user = user;
    }

    public async Task<Result> Handle(DeleteReportScheduleCommand request, CancellationToken ct)
    {
        var schedule = await ReportScheduleLookup.FindAsync(_platform, _user, request.Id, ct);
        if (schedule is null)
            return Result.Failure("Scheduled report not found.", 404);

        schedule.IsDeleted = true;
        schedule.IsActive = false;
        schedule.UpdatedAt = DateTime.UtcNow;
        schedule.UpdatedBy = _user.UserName;
        await _platform.SaveChangesAsync(ct);
        return Result.Success(204);
    }
}

// ─── Send now ──────────────────────────────────────────────────────────

/// <summary>
/// Sends the schedule's report straight away — to check the addresses and what arrives — without
/// moving its next regular run.
/// </summary>
public record SendReportScheduleNowCommand(Guid Id) : IRequest<Result<ReportScheduleDto>>, IRequirePermission
{
    public string RequiredPermission => "report.schedule";
}

public class SendReportScheduleNowHandler : IRequestHandler<SendReportScheduleNowCommand, Result<ReportScheduleDto>>
{
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _user;
    private readonly IScheduledReportSender _sender;

    public SendReportScheduleNowHandler(
        IPlatformDbContext platform, ICurrentUserService user, IScheduledReportSender sender)
    {
        _platform = platform;
        _user = user;
        _sender = sender;
    }

    public async Task<Result<ReportScheduleDto>> Handle(SendReportScheduleNowCommand request, CancellationToken ct)
    {
        var schedule = await ReportScheduleLookup.FindAsync(_platform, _user, request.Id, ct);
        if (schedule is null)
            return Result<ReportScheduleDto>.Failure("Scheduled report not found.", 404);

        var outcome = await _sender.SendAsync(schedule, ct);
        schedule.LastRunAt = DateTime.UtcNow;
        schedule.LastStatus = outcome.Status;
        schedule.LastError = outcome.Error;
        await _platform.SaveChangesAsync(ct);

        return outcome.Status switch
        {
            ReportScheduleStatus.Sent or ReportScheduleStatus.Partial =>
                Result<ReportScheduleDto>.Success(ReportScheduleDto.From(schedule)),
            // Email not set up on this server: nothing the person can fix from the form.
            ReportScheduleStatus.Skipped =>
                Result<ReportScheduleDto>.Failure(outcome.Error ?? "The report was not sent.", 503),
            _ => Result<ReportScheduleDto>.Failure(outcome.Error ?? "The report was not sent.", 502),
        };
    }
}

internal static class ReportScheduleLookup
{
    /// <summary>The schedule, if it belongs to the caller's agency. Another agency's id is a 404.</summary>
    public static async Task<ReportSchedule?> FindAsync(
        IPlatformDbContext platform, ICurrentUserService user, Guid id, CancellationToken ct)
    {
        if (user.TenantId is not Guid tenantId) return null;
        return await platform.ReportSchedules
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId && !s.IsDeleted, ct);
    }
}
