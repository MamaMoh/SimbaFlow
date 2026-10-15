using FluentAssertions;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// An agency leaves in order: offboard, export, purge. Purging is the one step nobody can undo, so
/// these are the checks that keep it from happening to an agency still at work, or to data nobody
/// has a copy of.
/// </summary>
public class OffboardingRulesTests
{
    private static readonly DateTime Offboarded = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static TenantInfo Agency(
        TenantStatus status = TenantStatus.Active,
        DateTime? offboardedAt = null,
        DateTime? exportedAt = null,
        DateTime? purgedAt = null) => new()
    {
        Id = Guid.Parse("8f14e45f-ceea-467a-9a36-dedd4bea2543"),
        Name = "Tango Foreign Employment Agent",
        Slug = "tango",
        SchemaName = "tenant_tango",
        SubscriptionStatus = status,
        OffboardedAt = offboardedAt,
        LastExportedAt = exportedAt,
        PurgedAt = purgedAt,
    };

    private static TenantInfo ReadyToPurge() =>
        Agency(TenantStatus.Deactivated, Offboarded, exportedAt: Offboarded.AddHours(1));

    private static TenantInfo Purged(TenantInfo agency)
    {
        agency.PurgedAt = Offboarded.AddDays(1);
        return agency;
    }

    [Fact]
    public void AnActiveOrSuspendedAgencyCanBeOffboarded()
    {
        OffboardingRules.OffboardRefusalFor(Agency()).Should().BeNull();
        OffboardingRules.OffboardRefusalFor(Agency(TenantStatus.Suspended)).Should().BeNull();
    }

    [Fact]
    public void AnAgencyIsNotOffboardedTwice()
    {
        OffboardingRules.OffboardRefusalFor(Agency(TenantStatus.Deactivated, Offboarded))
            .Should().Contain("already been offboarded");
    }

    [Fact]
    public void AnOffboardedAgencyCanComeBack()
    {
        OffboardingRules.ReactivateRefusalFor(Agency(TenantStatus.Deactivated, Offboarded)).Should().BeNull();
    }

    [Fact]
    public void APurgedAgencyCannotComeBack()
    {
        OffboardingRules.ReactivateRefusalFor(Purged(ReadyToPurge()))
            .Should().Contain("purged");
    }

    [Fact]
    public void AnAgencyStillInServiceCannotBePurged()
    {
        OffboardingRules.PurgeRefusalFor(Agency(), "tango").Should().Contain("Offboard");
        OffboardingRules.PurgeRefusalFor(Agency(TenantStatus.Suspended), "tango").Should().Contain("Offboard");
    }

    [Fact]
    public void AnAgencyWithNoExportCannotBePurged()
    {
        OffboardingRules.PurgeRefusalFor(Agency(TenantStatus.Deactivated, Offboarded), "tango")
            .Should().Contain("Export");
    }

    [Fact]
    public void AnExportFromBeforeOffboardingDoesNotCount()
    {
        // Anything done between that export and offboarding would be lost.
        var agency = Agency(TenantStatus.Deactivated, Offboarded, exportedAt: Offboarded.AddDays(-3));

        OffboardingRules.PurgeRefusalFor(agency, "tango").Should().Contain("Export");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Tango")]
    [InlineData("tang")]
    public void PurgingAsksForTheSlugExactly(string? typed)
    {
        OffboardingRules.PurgeRefusalFor(ReadyToPurge(), typed).Should().Contain("'tango'");
    }

    [Fact]
    public void AnOffboardedExportedConfirmedAgencyCanBePurged()
    {
        OffboardingRules.PurgeRefusalFor(ReadyToPurge(), " tango ").Should().BeNull();
    }

    [Fact]
    public void APurgedAgencyIsNotPurgedAgain()
    {
        OffboardingRules.PurgeRefusalFor(Purged(ReadyToPurge()), "tango")
            .Should().Contain("already been purged");
    }

    [Fact]
    public void ATombstoneFreesTheSlugAndSchemaName()
    {
        var (slug, schema) = OffboardingRules.TombstoneNames(ReadyToPurge());

        slug.Should().Be("tango~purged-8f14e45f");
        schema.Should().Be("tenant_tango~purged-8f14e45f");
    }
}
