using FluentAssertions;
using SimbaFlow.API.Features.Candidates;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

public class CandidateIntakeMapperTests
{
    [Fact]
    public void ExtraSkillsAreStoredOnTheCandidateAndBuiltInNamesAreIgnored()
    {
        var candidate = new Candidate();
        CandidateIntakeMapper.Apply(candidate, new CandidateIntakePayload(
            ExtraSkills:
            [
                new CandidateExtraSkill("Driving", true),
                new CandidateExtraSkill("Cleaning", true),
                new CandidateExtraSkill("  ", true),
                new CandidateExtraSkill("First aid", false),
            ]));

        candidate.ExtraSkills.Should().ContainSingle(s => s.Name == "Driving" && s.IsSelected);
        candidate.ExtraSkills.Should().NotContain(s => s.Name == BuiltInSkills.All[0].Name);
        candidate.ExtraSkills.Should().NotContain(s => s.Name == "First aid");
    }

    [Fact]
    public void UncheckingAnExtraSkillRemovesItOnTheNextSave()
    {
        var candidate = new Candidate();
        CandidateIntakeMapper.Apply(candidate, new CandidateIntakePayload(
            ExtraSkills: [new CandidateExtraSkill("Driving", true)]));
        CandidateIntakeMapper.Apply(candidate, new CandidateIntakePayload(
            ExtraSkills: [new CandidateExtraSkill("Driving", false)]));

        candidate.ExtraSkills.Single().IsDeleted.Should().BeTrue();
    }
}
