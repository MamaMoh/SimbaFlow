using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Auth;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The header names the agency you are working in. It could not, because the profile handed back at
/// sign-in carried no agency at all — so the badge fell through to the literal word "Agency" for
/// every user of the platform, which named nothing and read as a broken label.
/// </summary>
public class SignedInProfileTests : IDisposable
{
    private readonly PlatformDbContext _context;

    public SignedInProfileTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new PlatformDbContext(options, Substitute.For<ICurrentUserService>());
    }

    private async Task<Guid> GivenAgency(string name)
    {
        var id = Guid.NewGuid();
        _context.Tenants.Add(new TenantInfo
        {
            Id = id,
            Name = name,
            Slug = $"agency-{id:N}",
            SchemaName = $"tenant_{id:N}",
            SubscriptionStatus = TenantStatus.Active,
        });
        await _context.SaveChangesAsync();
        return id;
    }

    private static ApplicationUser Staff(Guid? tenantId, bool superAdmin = false) => new()
    {
        Id = Guid.NewGuid(),
        UserName = "mas",
        Email = "mas@gmail.com",
        FirstName = "mama",
        LastName = "Kebede",
        TenantId = tenantId,
        IsSuperAdmin = superAdmin,
    };

    [Fact]
    public async Task AUsersAgencyIsNamed_NotJustIdentified()
    {
        var agency = await GivenAgency("Tango Foreign Employment Agent");

        var profile = await SignedInProfile.BuildAsync(
            Staff(agency), [], ["EmbassyOfficer"], _context, default);

        profile.TenantId.Should().Be(agency);
        profile.TenantName.Should().Be(
            "Tango Foreign Employment Agent",
            "the id alone cannot be shown to a person");
    }

    [Fact]
    public async Task APlatformAccountHasNoAgency_AndSaysSoAsNull()
    {
        await GivenAgency("Tango Foreign Employment Agent");

        var profile = await SignedInProfile.BuildAsync(
            Staff(null, superAdmin: true), [], ["SuperAdmin"], _context, default);

        // Null rather than an empty string or a stand-in name: the header shows a picker for an
        // account with no agency and a label for one with an agency, so the two must be tellable
        // apart at a glance.
        profile.TenantId.Should().BeNull();
        profile.TenantName.Should().BeNull();
    }

    [Fact]
    public async Task AnAgencyThatIsNotThereLeavesTheNameNull_RatherThanFailingTheSignIn()
    {
        // Sign-in is already refused for a missing agency by TenantSignInGuard, so this is the
        // belt-and-braces case: building a profile must not be the thing that throws.
        var profile = await SignedInProfile.BuildAsync(
            Staff(Guid.NewGuid()), [], ["DataEntryClerk"], _context, default);

        profile.TenantName.Should().BeNull();
        profile.Username.Should().Be("mas");
    }

    [Fact]
    public async Task RolesAndPermissionsSurviveUnchanged()
    {
        var agency = await GivenAgency("Mamas");

        var profile = await SignedInProfile.BuildAsync(
            Staff(agency), ["candidates.read", "lmis.update"], ["EmbassyOfficer"], _context, default);

        profile.Permissions.Should().BeEquivalentTo(["candidates.read", "lmis.update"]);
        profile.Roles.Should().BeEquivalentTo(["EmbassyOfficer"]);
        profile.FullName.Should().Contain("Kebede");
    }

    public void Dispose() => _context.Dispose();
}
