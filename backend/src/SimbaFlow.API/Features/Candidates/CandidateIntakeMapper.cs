using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates;

/// <summary>
/// Optional EasyEnjaz-style intake fields shared by register/update.
/// </summary>
public record CandidateIntakePayload(
    string? LocalFullName = null,
    string? PlaceOfBirth = null,
    string? Religion = null,
    string? MaritalStatus = null,
    int? NumberOfChildren = null,
    string? Height = null,
    string? Weight = null,
    string? NationalId = null,
    string? BiometricId = null,
    string? PassportType = null,
    string? PassportPlaceOfIssue = null,
    string? PassportIssueDate = null,
    string? PassportExpiryDate = null,
    string? Region = null,
    string? Subcity = null,
    string? Woreda = null,
    string? HouseNo = null,
    string? Occupation = null,
    string? Qualification = null,
    string? MonthlySalary = null,
    string? ContractPeriod = null,
    string? EnglishLevel = null,
    string? ArabicLevel = null,
    string? OtherLanguages = null,
    int? ExperienceAbroadYears = null,
    string? WorksIn = null,
    string? Remark = null,
    string? CookingLevel = null,
    bool SkillCleaning = false,
    bool SkillWashing = false,
    bool SkillCooking = false,
    bool SkillIroning = false,
    bool SkillSewing = false,
    bool SkillArabicCooking = false,
    bool SkillTutoring = false,
    bool SkillComputer = false,
    string? Complexion = null,
    bool SkillBabysitting = false,
    bool SkillChildCare = false,
    string? VisaNumber = null,
    string? VisaType = null,
    string? SponsorName = null,
    string? SponsorIdNumber = null,
    string? SponsorPhone = null,
    string? SponsorAddress = null,
    string? SponsorArabicName = null,
    string? AgentName = null,
    string? ENumber = null,
    string? FileNo = null,
    string? WakalaNo = null,
    string? ContractNo = null,
    string? StickerVisaNo = null,
    string? RelativeName = null,
    string? RelativePhone = null,
    string? RelativeKinship = null,
    string? RelativeGender = null,
    string? RelativeBirthDate = null,
    string? RelativeCity = null,
    string? RelativeRegion = null,
    string? RelativeSubcity = null,
    string? RelativeWoreda = null,
    string? RelativeHouseNo = null,
    string? ContactPerson2 = null,
    string? ContactPhone2 = null,
    string? CocCenterName = null,
    string? CertificateNo = null,
    string? CertifiedDate = null,
    string? MedicalPlace = null,
    IReadOnlyList<CandidateExtraSkill>? ExtraSkills = null,
    /// <summary>
    /// Every posting abroad. Null means "not sent" and leaves the list alone; an empty list
    /// means the desk cleared it. The distinction matters because screens that save a candidate
    /// without touching their history would otherwise wipe it.
    /// </summary>
    IReadOnlyList<CandidateWorkExperienceEntry>? WorkExperiences = null);

/// <summary>One row of the work-experience table as the form sends it.</summary>
public record CandidateWorkExperienceEntry(string Country, string? Occupation = null, int? Years = null);

public record CandidateExtraSkill(string Name, bool Selected = true);

public static class CandidateIntakeMapper
{
    public static void Apply(Candidate candidate, CandidateIntakePayload p, bool setVisaDefault = false)
    {
        candidate.LocalFullName = NullIfEmpty(p.LocalFullName);
        candidate.PlaceOfBirth = NullIfEmpty(p.PlaceOfBirth);
        candidate.Religion = NullIfEmpty(p.Religion);
        candidate.MaritalStatus = NullIfEmpty(p.MaritalStatus);
        candidate.NumberOfChildren = p.NumberOfChildren;
        candidate.Height = NullIfEmpty(p.Height);
        candidate.Weight = NullIfEmpty(p.Weight);
        candidate.NationalId = NullIfEmpty(p.NationalId);
        candidate.BiometricId = NullIfEmpty(p.BiometricId);
        candidate.PassportType = NullIfEmpty(p.PassportType) ?? "Normal";
        candidate.PassportPlaceOfIssue = NullIfEmpty(p.PassportPlaceOfIssue);
        candidate.PassportIssueDate = ParseDate(p.PassportIssueDate);
        candidate.PassportExpiryDate = ParseDate(p.PassportExpiryDate);
        candidate.Region = NullIfEmpty(p.Region);
        candidate.Subcity = NullIfEmpty(p.Subcity);
        candidate.Woreda = NullIfEmpty(p.Woreda);
        candidate.HouseNo = NullIfEmpty(p.HouseNo);
        candidate.Occupation = NullIfEmpty(p.Occupation);
        candidate.Qualification = NullIfEmpty(p.Qualification);
        candidate.MonthlySalary = NullIfEmpty(p.MonthlySalary);
        candidate.ContractPeriod = NullIfEmpty(p.ContractPeriod) ?? "2 Years";
        candidate.EnglishLevel = NullIfEmpty(p.EnglishLevel);
        candidate.ArabicLevel = NullIfEmpty(p.ArabicLevel);
        candidate.OtherLanguages = NullIfEmpty(p.OtherLanguages);
        // Derived from WorkExperiences below whenever the caller sends that list, which the
        // registration form always does. A caller that sends neither the list nor these two is
        // saving a screen that never showed a work history, and must not erase one: assigning
        // the payload's nulls through here wiped the summary off every candidate anyone edited
        // from the visa or embassy screens.
        if (p.ExperienceAbroadYears is not null) candidate.ExperienceAbroadYears = p.ExperienceAbroadYears;
        if (NullIfEmpty(p.WorksIn) is string worksIn) candidate.WorksIn = worksIn;
        candidate.Remark = NullIfEmpty(p.Remark);
        candidate.CookingLevel = NullIfEmpty(p.CookingLevel);
        candidate.SkillCleaning = p.SkillCleaning;
        candidate.SkillWashing = p.SkillWashing;
        candidate.SkillCooking = p.SkillCooking;
        candidate.SkillIroning = p.SkillIroning;
        candidate.SkillSewing = p.SkillSewing;
        candidate.SkillArabicCooking = p.SkillArabicCooking;
        candidate.SkillTutoring = p.SkillTutoring;
        candidate.SkillComputer = p.SkillComputer;
        // Only when the caller actually says something. The intake form stopped asking for
        // complexion, so every save now arrives without it — and assigning that through would
        // erase the answer on file for every candidate anyone happened to edit.
        if (p.Complexion is not null)
            candidate.Complexion = NullIfEmpty(p.Complexion);
        candidate.SkillBabysitting = p.SkillBabysitting;
        candidate.SkillChildCare = p.SkillChildCare;
        if (p.ExtraSkills is not null)
            SyncExtraSkills(candidate, p.ExtraSkills);
        candidate.VisaNumber = NullIfEmpty(p.VisaNumber);
        candidate.VisaType = setVisaDefault
            ? (NullIfEmpty(p.VisaType) ?? "Work")
            : NullIfEmpty(p.VisaType);
        candidate.SponsorName = NullIfEmpty(p.SponsorName);
        candidate.SponsorIdNumber = NullIfEmpty(p.SponsorIdNumber);
        candidate.SponsorPhone = NullIfEmpty(p.SponsorPhone);
        candidate.SponsorAddress = NullIfEmpty(p.SponsorAddress);
        candidate.SponsorArabicName = NullIfEmpty(p.SponsorArabicName);
        candidate.AgentName = NullIfEmpty(p.AgentName);
        candidate.ENumber = NullIfEmpty(p.ENumber);
        candidate.FileNo = NullIfEmpty(p.FileNo);
        candidate.WakalaNo = NullIfEmpty(p.WakalaNo);
        candidate.ContractNo = NullIfEmpty(p.ContractNo);
        candidate.StickerVisaNo = NullIfEmpty(p.StickerVisaNo);
        candidate.RelativeName = NullIfEmpty(p.RelativeName);
        candidate.RelativePhone = NullIfEmpty(p.RelativePhone);
        candidate.RelativeKinship = NullIfEmpty(p.RelativeKinship);
        candidate.RelativeGender = NullIfEmpty(p.RelativeGender);
        candidate.RelativeBirthDate = ParseDate(p.RelativeBirthDate);
        candidate.RelativeCity = NullIfEmpty(p.RelativeCity);
        candidate.RelativeRegion = NullIfEmpty(p.RelativeRegion);
        candidate.RelativeSubcity = NullIfEmpty(p.RelativeSubcity);
        candidate.RelativeWoreda = NullIfEmpty(p.RelativeWoreda);
        candidate.RelativeHouseNo = NullIfEmpty(p.RelativeHouseNo);
        candidate.ContactPerson2 = NullIfEmpty(p.ContactPerson2);
        candidate.ContactPhone2 = NullIfEmpty(p.ContactPhone2);
        candidate.CocCenterName = NullIfEmpty(p.CocCenterName);
        candidate.CertificateNo = NullIfEmpty(p.CertificateNo);
        candidate.CertifiedDate = ParseDate(p.CertifiedDate);
        candidate.MedicalPlace = NullIfEmpty(p.MedicalPlace);

        // Last, because it writes back over WorksIn and ExperienceAbroadYears, which were
        // assigned above from the payload's own single-country fields. A caller that sends the
        // list is describing the whole history and wins; one that does not keeps what is there.
        if (p.WorkExperiences is not null)
            SyncWorkExperiences(candidate, p.WorkExperiences);
    }

    /// <summary>
    /// Replaces the candidate's postings with the ones sent, and re-derives the two summary
    /// columns from them.
    ///
    /// Rows are matched by position rather than by id: the form has no stable key for a line
    /// someone has just typed, and a country edited from Kuwait to Lebanon is the same row being
    /// corrected, not one row deleted and another added. Surplus rows are soft-deleted so a
    /// history that shrinks does not leave orphans on the board.
    /// </summary>
    public static void SyncWorkExperiences(
        Candidate candidate, IReadOnlyList<CandidateWorkExperienceEntry> entries)
    {
        var wanted = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Country))
            .Select(e => new CandidateWorkExperienceEntry(
                e.Country.Trim(), NullIfEmpty(e.Occupation), e.Years))
            .ToList();

        var rows = candidate.WorkExperiences
            .Where(w => !w.IsDeleted)
            .OrderBy(w => w.SortOrder)
            .ToList();

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < rows.Count)
            {
                rows[i].Country = wanted[i].Country;
                rows[i].Occupation = wanted[i].Occupation;
                rows[i].Years = wanted[i].Years;
                rows[i].SortOrder = i;
            }
            else
            {
                candidate.WorkExperiences.Add(new CandidateWorkExperience
                {
                    Country = wanted[i].Country,
                    Occupation = wanted[i].Occupation,
                    Years = wanted[i].Years,
                    SortOrder = i,
                });
            }
        }

        foreach (var surplus in rows.Skip(wanted.Count))
            surplus.IsDeleted = true;

        candidate.WorksIn = wanted.Count == 0
            ? null
            : string.Join(", ", wanted.Select(w => w.Country).Distinct(StringComparer.OrdinalIgnoreCase));

        // Summed, not maxed: two years in one country and three in another is five years of
        // experience, which is the number the salary is argued from.
        candidate.ExperienceAbroadYears = wanted.Any(w => w.Years is not null)
            ? wanted.Sum(w => w.Years ?? 0)
            : null;
    }

    public static void SyncExtraSkills(Candidate candidate, IReadOnlyList<CandidateExtraSkill> extras)
    {
        var selected = extras
            .Where(s => s.Selected && !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => s.Name.Trim())
            .Where(name => !BuiltInSkills.IsBuiltInName(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = candidate.ExtraSkills.ToList();
        foreach (var row in existing.Where(s => !s.IsDeleted))
        {
            if (!selected.Any(name => name.Equals(row.Name, StringComparison.OrdinalIgnoreCase)))
                row.IsDeleted = true;
        }

        foreach (var name in selected)
        {
            var row = existing.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                candidate.ExtraSkills.Add(new CandidateSkill { Name = name, IsSelected = true });
            }
            else
            {
                row.IsDeleted = false;
                row.IsSelected = true;
            }
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value);
}
