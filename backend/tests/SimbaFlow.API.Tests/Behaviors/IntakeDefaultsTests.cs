using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Tenants;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Persistence.Seeds;

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
        var dto = JsonSerializer.Serialize(IntakeDefaultsModule.ToDto(new AgencyIntakeDefaults
        {
            Gender = "1",
            Occupation = "NANNY",
            CvTemplate = "layout2",
        }, []));

        dto.Should().Contain("\"gender\":\"1\"");
        dto.Should().Contain("\"occupation\":\"NANNY\"");
        dto.Should().Contain("\"cvTemplate\":\"layout2\"");
        dto.Should().Contain("\"cvTemplates\":");
        dto.Should().Contain("\"skills\":");
        dto.Should().Contain("\"cookingLevel\":");
    }

    [Fact]
    public void SkillsInTheDtoUseCamelCaseNamesTheCardUnderstands()
    {
        var skill = new AgencySkill
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Driving",
            IsBuiltIn = false,
            IsDefaultSelected = true,
            SortOrder = 10,
        };

        var dto = JsonSerializer.Serialize(IntakeDefaultsModule.ToDto(new AgencyIntakeDefaults(), [skill]));

        dto.Should().Contain("\"name\":\"Driving\"");
        dto.Should().Contain("\"isDefaultSelected\":true");
        dto.Should().Contain("\"isBuiltIn\":false");
        dto.Should().Contain("\"builtInKey\":null");
    }

    [Fact]
    public async Task ASaveIsWrittenToTheIntakeDefaultsTableNotTheJsonBlob()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TenantDbContext(options, Substitute.For<ICurrentUserService>());

        await AgencyIntakeSeeder.EnsureAsync(db, new TenantSettings
        {
            Intake = new IntakeDefaults { Gender = "0", Occupation = "DRIVER" },
            Documents = new DocumentSettings { CvTemplate = "layout2" },
        });

        var row = await db.AgencyIntakeDefaults.SingleAsync();
        row.Gender.Should().Be("0");
        row.Occupation.Should().Be("DRIVER");
        row.CvTemplate.Should().Be("layout2");

        var skills = await db.AgencySkills.Where(s => !s.IsDeleted).ToListAsync();
        skills.Should().HaveCount(BuiltInSkills.All.Count);
        skills.Should().Contain(s => s.BuiltInKey == BuiltInSkills.Cleaning && s.IsDefaultSelected);
        skills.Should().Contain(s => s.BuiltInKey == BuiltInSkills.Washing && s.IsDefaultSelected);

        IntakeDefaultsModule.Apply(row, new IntakeDefaultsBody
        {
            Gender = "1",
            Occupation = "NANNY",
            Religion = "Muslim",
            Nationality = "Ethiopia",
            PassportType = "Official",
            MaritalStatus = "Married",
            CountryOfTravel = "Saudi Arabia",
            ContractPeriod = "2 Years",
            CookingLevel = "Good",
            CvTemplate = "layout2",
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.AgencyIntakeDefaults.SingleAsync();
        reloaded.Gender.Should().Be("1");
        reloaded.Occupation.Should().Be("NANNY");
        reloaded.Religion.Should().Be("Muslim");
        reloaded.CookingLevel.Should().Be("Good");
        reloaded.CvTemplate.Should().Be("layout2");
    }

    [Fact]
    public async Task CustomSkillsCanBeAddedAndTickedAsDefault()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new TenantDbContext(options, Substitute.For<ICurrentUserService>());
        await AgencyIntakeSeeder.EnsureAsync(db, new TenantSettings());

        var existing = await db.AgencySkills.Where(s => !s.IsDeleted).ToListAsync();
        var payload = existing.Select((s, i) => new IntakeSkillBody
        {
            Id = s.Id,
            Name = s.Name,
            IsDefaultSelected = s.BuiltInKey == BuiltInSkills.Cleaning,
            SortOrder = i,
        }).ToList();
        payload.Add(new IntakeSkillBody
        {
            Name = "Driving",
            IsDefaultSelected = true,
            SortOrder = payload.Count,
        });

        IntakeDefaultsModule.ApplySkills(db, existing, payload);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var skills = await db.AgencySkills.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToListAsync();
        skills.Should().Contain(s => s.Name == "Driving" && s.IsDefaultSelected && !s.IsBuiltIn);
        skills.Single(s => s.BuiltInKey == BuiltInSkills.Cleaning).IsDefaultSelected.Should().BeTrue();
        skills.Single(s => s.BuiltInKey == BuiltInSkills.Washing).IsDefaultSelected.Should().BeFalse();
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
