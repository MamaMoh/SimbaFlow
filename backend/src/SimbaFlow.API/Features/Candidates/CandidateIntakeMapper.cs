using SimbaFlow.Domain.Entities.Candidates;

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
    string? ReferenceNo = null,
    string? Remark = null,
    string? CookingLevel = null,
    bool SkillCleaning = false,
    bool SkillWashing = false,
    bool SkillCooking = false,
    bool SkillIroning = false,
    bool SkillSewing = false,
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
    string? ApplicationNo = null,
    string? FileNo = null,
    string? WakalaNo = null,
    string? ContractNo = null,
    string? StickerVisaNo = null,
    string? SignedOn = null,
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
    string? MedicalPlace = null);

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
        candidate.ExperienceAbroadYears = p.ExperienceAbroadYears;
        candidate.WorksIn = NullIfEmpty(p.WorksIn);
        candidate.ReferenceNo = NullIfEmpty(p.ReferenceNo);
        candidate.Remark = NullIfEmpty(p.Remark);
        candidate.CookingLevel = NullIfEmpty(p.CookingLevel);
        candidate.SkillCleaning = p.SkillCleaning;
        candidate.SkillWashing = p.SkillWashing;
        candidate.SkillCooking = p.SkillCooking;
        candidate.SkillIroning = p.SkillIroning;
        candidate.SkillSewing = p.SkillSewing;
        candidate.SkillBabysitting = p.SkillBabysitting;
        candidate.SkillChildCare = p.SkillChildCare;
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
        candidate.ApplicationNo = NullIfEmpty(p.ApplicationNo);
        candidate.FileNo = NullIfEmpty(p.FileNo);
        candidate.WakalaNo = NullIfEmpty(p.WakalaNo);
        candidate.ContractNo = NullIfEmpty(p.ContractNo);
        candidate.StickerVisaNo = NullIfEmpty(p.StickerVisaNo);
        candidate.SignedOn = ParseDate(p.SignedOn);
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
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value);
}
