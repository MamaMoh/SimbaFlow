using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Audit.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Audit;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The owner's audit trail. audit.read existed and both logs were written, but there was no page,
/// so an owner had no way to see who changed or opened what. Both tables are shared by every
/// agency, so the one thing this must never do is show one agency another's rows.
/// </summary>
public class AuditTrailTests : IDisposable
{
    private readonly PlatformDbContext _platform;
    private readonly TenantDbContext _tenant;
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly GetAuditTrailHandler _handler;

    private readonly Guid _agency = Guid.NewGuid();
    private readonly Guid _otherAgency = Guid.NewGuid();
    private readonly ApplicationUser _owner;
    private readonly ApplicationUser _clerk;
    private readonly Candidate _candidate;

    public AuditTrailTests()
    {
        _platform = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        _tenant = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _owner = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Hana", LastName = "Bekele", Email = "hana@a.et", TenantId = _agency };
        _clerk = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Dawit", LastName = "Alemu", Email = "dawit@a.et", TenantId = _agency };
        var stranger = new ApplicationUser { Id = Guid.NewGuid(), FirstName = "Other", LastName = "Agency", TenantId = _otherAgency };
        _platform.Users.AddRange(_owner, _clerk, stranger);

        _candidate = new Candidate { Id = Guid.NewGuid(), FirstName = "SEADA", LastName = "MEKONNEN", IsDeleted = true };
        _tenant.Candidates.Add(_candidate);
        _tenant.SaveChanges();

        var now = DateTime.UtcNow;
        _platform.AuditLogs.AddRange(
            Change(_clerk, "UpdateCandidateCommand", now.AddMinutes(-1), entityId: _candidate.Id.ToString()),
            Change(_owner, "CreateUserCommand", now.AddMinutes(-2)),
            Change(_clerk, "DeleteCandidateCommand", now.AddDays(-3), success: false),
            Change(stranger, "UpdateCandidateCommand", now, tenant: _otherAgency));
        _platform.ReadAuditLogs.AddRange(
            new ReadAuditLog { UserId = _clerk.Id.ToString(), TenantId = _agency, EntityType = "Candidate", EntityId = _candidate.Id.ToString(), Action = "GetCandidateByIdQuery", AccessedAt = now },
            new ReadAuditLog { UserId = stranger.Id.ToString(), TenantId = _otherAgency, EntityType = "Candidate", Action = "GetCandidatesQuery", AccessedAt = now });
        _platform.SaveChanges();

        _currentUser.TenantId.Returns(_agency);
        _handler = new GetAuditTrailHandler(_platform, _tenant, _currentUser);
    }

    private AuditLog Change(ApplicationUser by, string action, DateTime at, string? entityId = null, bool success = true, Guid? tenant = null) => new()
    {
        UserId = by.Id.ToString(),
        TenantId = tenant ?? _agency,
        Action = action,
        EntityType = action.Contains("Candidate") ? "Candidate" : "User",
        EntityId = entityId,
        Timestamp = at,
        Success = success,
    };

    [Fact]
    public async Task Changes_AreThisAgencysOnly_NewestFirst()
    {
        var result = await _handler.Handle(new GetAuditTrailQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Total.Should().Be(3);
        result.Data.Items.Select(i => i.RawAction).Should().Equal(
            "UpdateCandidateCommand", "CreateUserCommand", "DeleteCandidateCommand");
        result.Data.Items.Should().OnlyContain(i => i.UserId != null && i.UserName != "Other Agency");
    }

    [Fact]
    public async Task Views_AreThisAgencysOnly()
    {
        var result = await _handler.Handle(new GetAuditTrailQuery(AuditTrailKind.Views), CancellationToken.None);

        var only = result.Data!.Items.Should().ContainSingle().Subject;
        only.Action.Should().Be("Opened candidate");
        only.UserName.Should().Be("Dawit Alemu");
    }

    [Fact]
    public async Task ARemovedCandidate_IsStillNamed()
    {
        var result = await _handler.Handle(new GetAuditTrailQuery(AuditTrailKind.Views), CancellationToken.None);

        result.Data!.Items.Single().EntityLabel.Should().Be("SEADA MEKONNEN");
    }

    [Fact]
    public async Task Filters_ByPersonActionDateAndFailure()
    {
        (await _handler.Handle(new GetAuditTrailQuery(UserId: _owner.Id.ToString()), CancellationToken.None))
            .Data!.Items.Should().ContainSingle(i => i.RawAction == "CreateUserCommand");

        (await _handler.Handle(new GetAuditTrailQuery(Action: "DeleteCandidateCommand"), CancellationToken.None))
            .Data!.Total.Should().Be(1);

        (await _handler.Handle(new GetAuditTrailQuery(FailedOnly: true), CancellationToken.None))
            .Data!.Items.Should().ContainSingle(i => !i.Success);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await _handler.Handle(new GetAuditTrailQuery(From: today.AddDays(-1), To: today), CancellationToken.None))
            .Data!.Total.Should().Be(2);
    }

    [Fact]
    public async Task TheFilterLists_NameOnlyThisAgencysPeopleAndActions()
    {
        var result = await _handler.Handle(new GetAuditTrailQuery(), CancellationToken.None);

        result.Data!.Users.Select(u => u.Label).Should().BeEquivalentTo(["Dawit Alemu", "Hana Bekele"]);
        result.Data.Actions.Select(a => a.Value).Should().BeEquivalentTo(
            ["UpdateCandidateCommand", "CreateUserCommand", "DeleteCandidateCommand"]);
    }

    [Fact]
    public async Task Paging_IsCappedAndCounted()
    {
        var result = await _handler.Handle(new GetAuditTrailQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Data!.Items.Should().ContainSingle();
        result.Data.Total.Should().Be(3);

        (await _handler.Handle(new GetAuditTrailQuery(PageSize: 10_000), CancellationToken.None))
            .Data!.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task WithNoAgencySelected_ItRefusesRatherThanShowingEveryone()
    {
        _currentUser.TenantId.Returns((Guid?)null);

        var result = await _handler.Handle(new GetAuditTrailQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public void ItNeedsAuditRead()
    {
        new GetAuditTrailQuery().RequiredPermission.Should().Be("audit.read");
    }

    [Theory]
    [InlineData("UpdateCandidateCommand", "Update candidate")]
    [InlineData("AssignRolesCommand", "Assign roles")]
    [InlineData("SetLMISStatusCommand", "Set LMIS status")]
    [InlineData("GenerateCVCommand", "Printed CV")]
    [InlineData("GetCandidateByIdQuery", "Opened candidate")]
    public void Actions_ReadAsWords(string action, string expected)
    {
        AuditActionNames.Describe(action).Should().Be(expected);
    }

    public void Dispose()
    {
        _platform.Dispose();
        _tenant.Dispose();
    }
}
