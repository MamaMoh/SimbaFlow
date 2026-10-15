using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.API.Features.Audit.Queries;
using SimbaFlow.API.Features.Auth.Commands;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.API.Features.Reports.Queries;
using SimbaFlow.API.Features.Staff.Commands;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The change log. It used to record a request only when its name began with Create, Update,
/// Delete or Set (or held Toggle, Assign, Reset, Revoke or Import), so payments, voids, stage
/// moves, sign-ins, suspensions and terminations left no trace; a request that failed with a
/// Result was written down as a success; and nothing said what was asked for.
/// </summary>
public class AuditBehaviorTests
{
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly Guid _tenant = Guid.NewGuid();

    public AuditBehaviorTests()
    {
        _currentUser.UserId.Returns("user-1");
        _currentUser.TenantId.Returns(_tenant);
        _currentUser.IpAddress.Returns("10.0.0.7");
    }

    private Task<TResponse> Run<TRequest, TResponse>(TRequest request, Func<TResponse> handler)
        where TRequest : notnull
    {
        var behavior = new AuditBehavior<TRequest, TResponse>(
            _audit, _currentUser, NullLogger<AuditBehavior<TRequest, TResponse>>.Instance);
        return behavior.Handle(request, _ => Task.FromResult(handler()), CancellationToken.None);
    }

    private AuditEntry Logged() =>
        (AuditEntry)_audit.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IAuditService.LogAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task VoidingAPayment_IsRecorded_WithWhoWhereAndWhichPayment()
    {
        var payment = Guid.NewGuid();

        await Run(new VoidPaymentCommand(payment, "Entered twice"), () => Result.Success());

        var entry = Logged();
        entry.Action.Should().Be(nameof(VoidPaymentCommand));
        entry.EntityType.Should().Be("Payment");
        entry.EntityId.Should().Be(payment.ToString());
        entry.UserId.Should().Be("user-1");
        entry.TenantId.Should().Be(_tenant);
        entry.IpAddress.Should().Be("10.0.0.7");
        entry.Success.Should().BeTrue();
        entry.NewValues.Should().Contain("Entered twice");
    }

    [Fact]
    public async Task SuspendingStaff_IsRecorded()
    {
        var staff = Guid.NewGuid();

        await Run(new SuspendStaffCommand(staff, "Absent"), () => Result.Success());

        Logged().EntityId.Should().Be(staff.ToString());
    }

    [Fact]
    public async Task ARefusedResult_IsRecordedAsAFailure()
    {
        await Run(new VoidPaymentCommand(Guid.NewGuid(), "x"), () => Result.Failure("Already void", 409));

        Logged().Success.Should().BeFalse();
    }

    [Fact]
    public async Task AnAttemptAMissingPermissionRefused_IsRecordedAsAFailure()
    {
        var act = () => Run<VoidPaymentCommand, Result>(
            new VoidPaymentCommand(Guid.NewGuid(), "x"),
            () => throw new ForbiddenAccessException("no"));

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        Logged().Success.Should().BeFalse();
    }

    [Fact]
    public async Task ASignIn_RecordsTheUsername_AndNeverThePassword()
    {
        await Run(new LoginCommand("abebe", "hunter2-secret"),
            () => Result<LoginResponse>.Failure("Invalid credentials", 401));

        var entry = Logged();
        entry.Success.Should().BeFalse();
        entry.NewValues.Should().Contain("abebe");
        entry.NewValues.Should().NotContain("hunter2");
    }

    [Fact]
    public async Task ReadsAreNotOnTheChangeLog_ButExportsAre()
    {
        await Run(new GetReportQuery("pipeline"), () => Result<ReportTable>.Failure("x"));
        _audit.ReceivedCalls().Should().BeEmpty();

        await Run(new ExportReportQuery("pipeline", "excel"), () => Result<ReportFile>.Failure("x"));
        Logged().Action.Should().Be(nameof(ExportReportQuery));
    }

    [Fact]
    public async Task ATokenRefresh_IsNotRecorded()
    {
        await Run(new RefreshTokenCommand("token"), () => Result<RefreshResponse>.Failure("x"));

        _audit.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public void EveryCommandInTheApi_IsRecorded()
    {
        var skipped = typeof(Program).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i =>
                i == typeof(IBaseRequest)))
            .Select(t => t.Name)
            .Where(n => !n.EndsWith("Query", StringComparison.Ordinal) && !AuditDetails.IsRecorded(n))
            .ToList();

        skipped.Should().BeEquivalentTo(["RefreshTokenCommand"]);
    }

    [Fact]
    public void Details_BlankSecretsAtAnyDepth_AndSummariseFilesAndLongText()
    {
        var json = AuditDetails.Describe(new
        {
            Name = "Office A",
            NewPassword = "p@ss",
            Nested = new { RefreshToken = "t", Code = "123456", Keep = "yes" },
            File = new byte[2048],
            Notes = new string('x', 2000),
        });

        json.Should().Contain("Office A").And.Contain("yes")
            .And.Contain("[file, 2048 bytes]").And.Contain("[2000 characters]");
        json.Should().NotContain("p@ss").And.NotContain("123456").And.NotContain("\"t\"");
    }

    [Fact]
    public void Details_ThatAreTooLong_KeepOnlyTheFieldNames()
    {
        var json = AuditDetails.Describe(new
        {
            Ids = Enumerable.Range(0, 49).Select(_ => new string('a', 400)).ToList(),
        });

        json.Should().Contain("\"truncated\":true").And.Contain("ids");
        json!.Length.Should().BeLessThan(AuditDetails.MaxLength);
    }

    [Theory]
    [InlineData("HTTP PUT /api/offices/{id}", "Changed offices")]
    [InlineData("HTTP DELETE /api/branding/agency/{kind}", "Removed branding agency")]
    [InlineData("HTTP POST /api/bot/link-code", "Saved bot link code")]
    [InlineData("RecordPaymentCommand", "Record payment")]
    [InlineData("LoginCommand", "Signed in")]
    public void TheAuditPage_WordsEveryKindOfRow(string action, string expected) =>
        AuditActionNames.Describe(action).Should().Be(expected);
}
