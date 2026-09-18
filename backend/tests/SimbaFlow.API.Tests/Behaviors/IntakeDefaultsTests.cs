using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Tenants;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The answers a new registration starts with.
///
/// Saving them used to toast success and then vanish: the JSON blob EF stores for TenantSettings
/// did not count as changed, and the create form applied a stale empty payload once and then
/// ignored the real values. These tests pin the save round-trip and the camelCase body the
/// settings page actually sends.
/// </summary>
public class IntakeDefaultsTests : IDisposable
{
    private readonly ProbeContext _db = new(
        new DbContextOptionsBuilder<ProbeContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void ACamelCaseSettingsSaveBindsEveryFieldIncludingTheLayout()
    {
        var body = JsonSerializer.Deserialize<IntakeDefaultsBody>(
            """
            {
              "gender": "1",
              "occupation": "NANNY",
              "religion": "Muslim",
              "nationality": "Ethiopia",
              "passportType": "Normal",
              "maritalStatus": "Married",
              "countryOfTravel": "Saudi Arabia",
              "contractPeriod": "2 Years",
              "cvTemplate": "layout2"
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        body.Should().NotBeNull();
        var settings = IntakeDefaultsModule.Apply(new TenantSettings(), body!);

        settings.Intake.Gender.Should().Be("1");
        settings.Intake.Occupation.Should().Be("NANNY");
        settings.Intake.Religion.Should().Be("Muslim");
        settings.Intake.Nationality.Should().Be("Ethiopia");
        settings.Intake.PassportType.Should().Be("Normal");
        settings.Intake.MaritalStatus.Should().Be("Married");
        settings.Intake.CountryOfTravel.Should().Be("Saudi Arabia");
        settings.Intake.ContractPeriod.Should().Be("2 Years");
        settings.Documents.CvTemplate.Should().Be("layout2");
    }

    [Fact]
    public void ClearingADefaultIsKeptRatherThanRestoredToTheBuiltInStartingPoint()
    {
        var current = new TenantSettings
        {
            Intake = new IntakeDefaults { Gender = "1", Occupation = "HOUSE MAID" },
        };

        var settings = IntakeDefaultsModule.Apply(current, new IntakeDefaultsBody
        {
            Gender = "",
            Occupation = "",
            CvTemplate = "layout2",
        });

        settings.Intake.Gender.Should().BeEmpty();
        settings.Intake.Occupation.Should().BeEmpty();
        settings.Documents.CvTemplate.Should().Be("layout2");
    }

    [Fact]
    public void StoredCamelCaseJsonStillReadsIntakeFields()
    {
        var json = """{"intake":{"gender":"0","occupation":"COOK","nationality":"Ethiopia","contractPeriod":"2 Years"},"documents":{"cvTemplate":"layout2"}}""";

        var settings = TenantSettingsJson.Deserialize(json);

        settings.Intake.Gender.Should().Be("0");
        settings.Intake.Occupation.Should().Be("COOK");
        settings.Documents.CvTemplate.Should().Be("layout2");
    }

    [Fact]
    public void TheDtoTheBrowserReadsUsesCamelCaseNames()
    {
        var dto = JsonSerializer.Serialize(IntakeDefaultsModule.ToDto(new TenantSettings
        {
            Intake = new IntakeDefaults { Gender = "1", Occupation = "NANNY" },
            Documents = new DocumentSettings { CvTemplate = "layout2" },
        }));

        dto.Should().Contain("\"gender\":\"1\"");
        dto.Should().Contain("\"occupation\":\"NANNY\"");
        dto.Should().Contain("\"cvTemplate\":\"layout2\"");
        dto.Should().Contain("\"cvTemplates\":");
    }

    [Fact]
    public async Task ReplacingIntakeOnALoadedTenantIsWrittenBack()
    {
        var id = Guid.NewGuid();
        _db.Tenants.Add(new TenantInfo
        {
            Id = id,
            Name = "Tango",
            Slug = "tango",
            SchemaName = "tenant_tango",
            ContactEmail = "a@b.c",
            Settings = new TenantSettings(),
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var tenant = await _db.Tenants.FirstAsync(t => t.Id == id);
        tenant.Settings = IntakeDefaultsModule.Apply(tenant.Settings, new IntakeDefaultsBody
        {
            Gender = "1",
            Occupation = "NANNY",
            Religion = "Muslim",
            Nationality = "Ethiopia",
            PassportType = "Official",
            MaritalStatus = "Married",
            CountryOfTravel = "Saudi Arabia",
            ContractPeriod = "2 Years",
            CvTemplate = "layout2",
        });
        _db.Entry(tenant).Property(t => t.Settings).IsModified = true;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var reloaded = await _db.Tenants.FirstAsync(t => t.Id == id);
        reloaded.Settings.Intake.Gender.Should().Be("1");
        reloaded.Settings.Intake.Occupation.Should().Be("NANNY");
        reloaded.Settings.Intake.Religion.Should().Be("Muslim");
        reloaded.Settings.Documents.CvTemplate.Should().Be("layout2");
    }

    /// <summary>Mirrors the TenantSettings conversion on PlatformDbContext, without Identity.</summary>
    private sealed class ProbeContext : DbContext
    {
        public ProbeContext(DbContextOptions<ProbeContext> options) : base(options) { }

        public DbSet<TenantInfo> Tenants => Set<TenantInfo>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantInfo>(entity =>
            {
                entity.Property(t => t.Settings)
                    .HasConversion(
                        v => TenantSettingsJson.Serialize(v),
                        v => TenantSettingsJson.Deserialize(v),
                        TenantSettingsJson.Comparer);
                entity.Property(t => t.RowVersion).ValueGeneratedNever();
            });
        }
    }
}
