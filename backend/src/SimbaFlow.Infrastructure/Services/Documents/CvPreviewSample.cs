using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// The stand-in candidate a layout preview is drawn with.
///
/// Every field is filled, including the ones most real records leave blank — a preview exists to
/// show how the form looks when it is full, not how it looks for one particular person. The name
/// says SAMPLE so a preview can never be mistaken for somebody's actual paperwork.
/// </summary>
internal static class CvPreviewSample
{
    internal static Candidate Candidate() => new()
    {
        Id = Guid.Empty,
        FirstName = "Sample",
        LastName = "Candidate",
        LocalFullName = "ናሙና እጩ",
        Gender = Gender.Female,
        DateOfBirth = new DateOnly(1994, 3, 18),
        PlaceOfBirth = "ARSI",
        Nationality = "Ethiopia",
        Religion = "Muslim",
        MaritalStatus = "Single",
        NumberOfChildren = 0,
        Height = "165 cm",
        Weight = "58 kg",
        PassportNumber = "EP0000000",
        PassportType = "Normal",
        PassportPlaceOfIssue = "ADDIS ABABA",
        PassportIssueDate = new DateOnly(2024, 6, 14),
        PassportExpiryDate = new DateOnly(2029, 6, 13),

        PhoneNumber = "+251 91 000 0000",
        Address = "Bole",
        City = "ADDIS ABABA",
        Region = "Addis Ababa",
        Country = "Ethiopia",

        EnglishLevel = "Good",
        ArabicLevel = "Fair",
        Qualification = "HIGH SCHOOL",

        Occupation = "HOUSE MAID",
        MonthlySalary = "1,000 SR",
        ContractPeriod = "2 Years",
        CountryOfTravel = "Saudi Arabia",
        WorksIn = "Saudi Arabia",
        ExperienceAbroadYears = 2,
        CookingLevel = "Good",

        SkillCleaning = true,
        SkillWashing = true,
        SkillCooking = true,
        SkillIroning = true,
        SkillBabysitting = true,
        SkillChildCare = false,
        SkillSewing = false,
        SkillArabicCooking = false,
        SkillTutoring = false,
        SkillComputer = false,

        ReferenceNo = "SAMPLE-001",
        LabourId = "LAB-0000",
        Remark = "This is a sample used to preview the layout.",
        // Deliberately no PartnerName: a preview shows the agency its own paperwork, and the
        // letterhead falls back to whatever name is on the record — a partner's name at the top
        // would suggest branding that is not theirs.
    };
}
