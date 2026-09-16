using FluentAssertions;
using SimbaFlow.API.Features.Tenants.Commands;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The slug becomes the tenant's PostgreSQL schema name, and that name is interpolated into
/// "SET search_path" on every request the tenant makes. It had no validator at all.
/// </summary>
public class TenantSlugValidationTests
{
    private static ProvisionTenantCommand WithSlug(string slug) =>
        new("Agency", slug, "a@b.et", null, "First", "Last", "admin@b.et", "Passw0rd!");

    private static bool SlugAccepted(string slug) =>
        new ProvisionTenantValidator().Validate(WithSlug(slug))
            .Errors.All(e => e.PropertyName != nameof(ProvisionTenantCommand.Slug));

    [Theory]
    // Every slug in use today, so the rule does not reject the agencies already on the platform.
    [InlineData("default-agency")]
    [InlineData("demo-agency")]
    [InlineData("khas-foreign-employment-agent")]
    [InlineData("mamas")]
    [InlineData("umukhalid-foreign-employment-agent")]
    public void RealSlugsAreAccepted(string slug) => SlugAccepted(slug).Should().BeTrue();

    [Theory]
    [InlineData("a\"; DROP SCHEMA public CASCADE; --", "a quote closes the quoted identifier")]
    [InlineData("Tango", "uppercase does not survive an unquoted identifier intact")]
    [InlineData("tango agency", "a space is not valid in a bare schema name")]
    [InlineData("tango_agency", "underscores are added when the schema name is built, not before")]
    [InlineData("1agency", "an identifier may not begin with a digit")]
    [InlineData("-agency", "nor with a hyphen")]
    [InlineData("agency-", "nor end with one")]
    [InlineData("ab", "too short to be meaningful")]
    [InlineData("", "empty")]
    public void DangerousOrMalformedSlugsAreRefused(string slug, string why) =>
        SlugAccepted(slug).Should().BeFalse(why);

    [Fact]
    public void AReservedSchemaNameIsHarmlessBecauseOfThePrefix()
    {
        // Worth stating: a slug of "public" does not produce the platform schema. The handler builds
        // "tenant_" + slug, so it becomes tenant_public and collides with nothing.
        SlugAccepted("public").Should().BeTrue();
    }

    [Fact]
    public void ASlugCannotOverflowThePostgresIdentifierLimit()
    {
        // "tenant_" plus the slug has to fit in 63 bytes, or Postgres silently truncates — and two
        // agencies whose names agree for the first 56 characters would share a schema.
        var tooLong = new string('a', 64);

        SlugAccepted(tooLong).Should().BeFalse();
        SlugAccepted(new string('a', 40)).Should().BeTrue();
        ("tenant_" + new string('a', 40)).Length.Should().BeLessThan(63);
    }
}
