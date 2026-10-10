using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Tenants;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// An agency's own list of skills, including the ones it was given to start with.
///
/// The built-in ten could not be removed: an agency that never places cooks still had Cooking
/// and Arabic cooking on every registration form, and the X that removes a skill appeared only
/// on ones they had added themselves. They are the agency's form, so they are the agency's to
/// shorten — and to put back, which is the half that is easy to get wrong.
/// </summary>
public class AgencySkillRemovalTests : IAsyncLifetime
{
    private TenantDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        await AgencyIntakeSeeder.EnsureAsync(_db, new TenantSettings());
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private Task<List<AgencySkill>> LiveAsync() =>
        _db.AgencySkills.Where(s => !s.IsDeleted).OrderBy(s => s.SortOrder).ToListAsync();

    private async Task SaveAsync(IReadOnlyList<AgencySkill> keep)
    {
        var existing = await _db.AgencySkills.ToListAsync();
        IntakeDefaultsModule.ApplySkills(_db, existing, [.. keep.Select((s, i) => new IntakeSkillBody
        {
            Id = s.Id == Guid.Empty ? null : s.Id,
            Name = s.Name,
            IsDefaultSelected = s.IsDefaultSelected,
            SortOrder = i,
        })]);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ABuiltInSkillCanBeTakenOffTheForm()
    {
        var live = await LiveAsync();
        var cooking = live.Single(s => s.BuiltInKey == BuiltInSkills.Cooking);

        await SaveAsync([.. live.Where(s => s.Id != cooking.Id)]);

        (await LiveAsync()).Should().NotContain(s => s.BuiltInKey == BuiltInSkills.Cooking);
    }

    [Fact]
    public async Task ARemovedBuiltInStaysRemovedWhenTheSettingsPageIsOpenedAgain()
    {
        // The seeder tops up missing built-ins on every load. Checking only the live rows meant
        // it put the removed one straight back, and the removal appeared not to have saved.
        var live = await LiveAsync();
        var cooking = live.Single(s => s.BuiltInKey == BuiltInSkills.Cooking);
        await SaveAsync([.. live.Where(s => s.Id != cooking.Id)]);

        await AgencyIntakeSeeder.EnsureAsync(_db, new TenantSettings());

        (await LiveAsync()).Should().NotContain(s => s.BuiltInKey == BuiltInSkills.Cooking);
    }

    [Fact]
    public async Task TypingARemovedBuiltInBackRestoresTheOriginalRatherThanMakingACopy()
    {
        var live = await LiveAsync();
        var cooking = live.Single(s => s.BuiltInKey == BuiltInSkills.Cooking);
        var originalId = cooking.Id;
        await SaveAsync([.. live.Where(s => s.Id != cooking.Id)]);

        // Added by name with no id, the way the "Add a skill" box sends it.
        var remaining = await LiveAsync();
        await SaveAsync([.. remaining, new AgencySkill { Name = cooking.Name, IsDefaultSelected = true }]);

        var back = (await LiveAsync()).Where(s => s.Name == cooking.Name).ToList();
        back.Should().ContainSingle("the skill comes back once, not twice");
        back[0].Id.Should().Be(originalId, "it is the same skill, not a custom one of the same name");
        back[0].IsBuiltIn.Should().BeTrue("so the candidate column behind it still applies");
    }

    [Fact]
    public async Task ACustomSkillCanStillBeAddedAndRemoved()
    {
        var live = await LiveAsync();
        await SaveAsync([.. live, new AgencySkill { Name = "Driving", IsDefaultSelected = true }]);

        var withDriving = await LiveAsync();
        withDriving.Should().Contain(s => s.Name == "Driving" && !s.IsBuiltIn);

        await SaveAsync([.. withDriving.Where(s => s.Name != "Driving")]);

        (await LiveAsync()).Should().NotContain(s => s.Name == "Driving");
    }
}
