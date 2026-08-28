using SimbaFlow.API.Features.Candidates;

namespace SimbaFlow.API.Features.SampleData;

/// <summary>
/// One sample candidate: who they are, and where in the pipeline they should end up.
///
/// The seeder does not write stages or statuses directly — it registers each candidate and then
/// drives them forward through the real workflow engine, so the event log, the mirror views and the
/// board visibility are all produced the same way a member of staff would produce them. A sample
/// candidate is therefore indistinguishable from a real one except for the SMP- application number,
/// which is what makes them removable again.
/// </summary>
public sealed record SamplePerson(
    string ApplicationNo,
    string FirstName,
    string MiddleName,
    string LastName,
    string LocalName,
    string Passport,
    string BirthDate,
    int Gender,
    string Phone,
    string City,
    string Occupation,
    string Salary,
    SampleTarget Target,
    string Note);

/// <summary>Where a sample candidate should come to rest.</summary>
public enum SampleTarget
{
    /// <summary>Registered, not yet reviewed.</summary>
    Intake,
    NewContractsPending,
    NewContractsReady,
    /// <summary>On the Embassy board, both tracks still pending.</summary>
    EmbassyFresh,
    /// <summary>Medical done, tasheer part-way.</summary>
    EmbassyMedicalFit,
    /// <summary>Visa Ready — mirrors onto the Case Executive board.</summary>
    EmbassyVisaReady,
    /// <summary>Fit + Book Done — mirrors onto the LMIS board while staying in Embassy.</summary>
    EmbassyMirrorsToLmis,
    /// <summary>Medically unfit: withdrawn from the pipeline.</summary>
    EmbassyUnfit,
    /// <summary>Moved into LMIS proper, insurance not yet paid.</summary>
    LmisInsuranceUnpaid,
    /// <summary>Insurance paid, LMIS document uploaded.</summary>
    LmisUploaded,
    Ticket,
    Departure,
    ArrivalPending,
    /// <summary>Arrived and carried into Commission.</summary>
    ArrivalArrivedWithCommission,
    /// <summary>Runaway — opens an exception case.</summary>
    ArrivalRunaway,
    /// <summary>Returned early — opens an exception case.</summary>
    ArrivalReturned
}

public static class SampleDataSpec
{
    /// <summary>Application numbers all start with this, which is how removal finds them again.</summary>
    public const string Prefix = "SMP-";

    /// <summary>
    /// Sixteen candidates covering every board in the sidebar, including the two mirror views and
    /// both exception types. Deliberately varied — different ages, destinations, occupations and
    /// missing fields — so the boards, filters and documents have something realistic to chew on.
    /// </summary>
    public static readonly SamplePerson[] People =
    [
        new("SMP-0001", "Almaz", "Getachew", "Tesfaye", "አልማዝ ጌታቸው", "EP4410233", "1998-04-12", 1,
            "+251911400001", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.Intake, "Just registered, awaiting review"),

        new("SMP-0002", "Hanan", "Yusuf", "Ahmed", "ሃናን ዩሱፍ", "EP4410234", "2000-09-30", 1,
            "+251911400002", "Dire Dawa", "House Maid", "1000 SAR",
            SampleTarget.Intake, "Just registered, awaiting review"),

        new("SMP-0003", "Meseret", "Bekele", "Alemu", "መሰረት በቀለ", "EP4410235", "1996-01-22", 1,
            "+251911400003", "Bahir Dar", "Cleaner", "1100 SAR",
            SampleTarget.NewContractsPending, "Contract under review"),

        new("SMP-0004", "Rahel", "Girma", "Wolde", "ራሔል ግርማ", "EP4410236", "1999-07-05", 1,
            "+251911400004", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.NewContractsReady, "Contract ready — can be pushed to Embassy"),

        new("SMP-0005", "Zeytuna", "Kedir", "Nuru", "ዘይቱና ከድር", "EP4410237", "1997-11-18", 1,
            "+251911400005", "Adama", "House Maid", "1200 SAR",
            SampleTarget.EmbassyFresh, "On Embassy board, nothing booked yet"),

        new("SMP-0006", "Tigist", "Haile", "Mengistu", "ትዕግስት ኃይሌ", "EP4410238", "1995-03-09", 1,
            "+251911400006", "Hawassa", "Nanny", "1300 SAR",
            SampleTarget.EmbassyMedicalFit, "Medical fit, tasheer booked but not done"),

        new("SMP-0007", "Bethlehem", "Assefa", "Kebede", "ቤተልሔም አሰፋ", "EP4410239", "2001-06-14", 1,
            "+251911400007", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.EmbassyVisaReady, "Visa Ready — mirrors onto Case Executive"),

        new("SMP-0008", "Feven", "Solomon", "Abera", "ፌቨን ሰለሞን", "EP4410240", "1998-12-01", 1,
            "+251911400008", "Mekelle", "House Maid", "1200 SAR",
            SampleTarget.EmbassyMirrorsToLmis, "Fit + Book Done — mirrors onto LMIS"),

        new("SMP-0009", "Selam", "Tadesse", "Gebre", "ሰላም ታደሰ", "EP4410241", "1994-08-27", 1,
            "+251911400009", "Gondar", "House Maid", "1000 SAR",
            SampleTarget.EmbassyUnfit, "Medically unfit — withdrawn, shows as Inactive"),

        new("SMP-0010", "Mekdes", "Worku", "Desta", "መቅደስ ወርቁ", "EP4410242", "1999-02-16", 1,
            "+251911400010", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.LmisInsuranceUnpaid, "In LMIS, insurance unpaid"),

        new("SMP-0011", "Sara", "Mohammed", "Ali", "ሳራ መሐመድ", "EP4410243", "1997-05-23", 1,
            "+251911400011", "Jimma", "House Maid", "1200 SAR",
            SampleTarget.LmisUploaded, "Insurance paid, LMIS uploaded"),

        new("SMP-0012", "Genet", "Abush", "Lemma", "ገነት አቡሽ", "EP4410244", "1996-10-08", 1,
            "+251911400012", "Addis Ababa", "House Maid", "1300 SAR",
            SampleTarget.Ticket, "LMIS issued — waiting on a ticket"),

        new("SMP-0013", "Leyla", "Abdurahman", "Hussein", "ለይላ አብዱራህማን", "EP4410245", "2000-01-19", 1,
            "+251911400013", "Harar", "House Maid", "1200 SAR",
            SampleTarget.Departure, "Ticket booked — awaiting departure"),

        new("SMP-0014", "Aster", "Negash", "Tilahun", "አስቴር ነጋሽ", "EP4410246", "1995-09-11", 1,
            "+251911400014", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.ArrivalPending, "Departed — arrival not yet confirmed"),

        new("SMP-0015", "Kalkidan", "Fikru", "Mamo", "ቃልኪዳን ፍቅሩ", "EP4410247", "1998-07-02", 1,
            "+251911400015", "Addis Ababa", "House Maid", "1400 SAR",
            SampleTarget.ArrivalArrivedWithCommission, "Arrived — commission due"),

        new("SMP-0016", "Yordanos", "Berhanu", "Tsegaye", "ዮርዳኖስ ብርሃኑ", "EP4410248", "1997-04-25", 1,
            "+251911400016", "Addis Ababa", "House Maid", "1200 SAR",
            SampleTarget.ArrivalRunaway, "Runaway — exception case open"),

        new("SMP-0017", "Eden", "Wondimu", "Regassa", "ኤደን ወንድሙ", "EP4410249", "1999-11-07", 1,
            "+251911400017", "Shashemene", "House Maid", "1100 SAR",
            SampleTarget.ArrivalReturned, "Returned early — exception case open"),
    ];

    /// <summary>Intake payload for a sample person — enough filled in that the documents render.</summary>
    public static CandidateIntakePayload Intake(SamplePerson p, int index) => new(
        LocalFullName: p.LocalName,
        PlaceOfBirth: p.City,
        Religion: index % 3 == 0 ? "Muslim" : "Orthodox",
        MaritalStatus: index % 4 == 0 ? "Married" : "Single",
        NumberOfChildren: index % 4 == 0 ? 1 : 0,
        Height: $"1.{60 + (index % 15)}",
        Weight: $"{52 + (index % 12)}",
        PassportType: "Normal",
        PassportPlaceOfIssue: "Addis Ababa",
        PassportIssueDate: "2024-03-15",
        PassportExpiryDate: "2029-03-14",
        Region: "Addis Ababa",
        Subcity: "Bole",
        Woreda: $"{03 + (index % 9)}",
        Occupation: p.Occupation,
        Qualification: index % 2 == 0 ? "Grade 10" : "Grade 8",
        MonthlySalary: p.Salary,
        ContractPeriod: "2 Years",
        EnglishLevel: index % 3 == 0 ? "Good" : "Fair",
        ArabicLevel: index % 4 == 0 ? "Fair" : "None",
        ExperienceAbroadYears: index % 5,
        WorksIn: "Riyadh",
        SkillCleaning: true,
        SkillWashing: true,
        SkillCooking: index % 2 == 0,
        SkillIroning: true,
        SkillBabysitting: index % 3 == 0,
        SkillChildCare: index % 3 == 0,
        VisaNumber: $"V-{7100 + index}",
        VisaType: "Domestic Worker",
        SponsorName: "Abdullah Al-Otaibi",
        SponsorArabicName: "عبدالله العتيبي",
        SponsorIdNumber: $"10{45678900 + index}",
        SponsorPhone: "+96650{0}0000".Replace("{0}", (100000 + index).ToString()[..6]),
        SponsorAddress: "Riyadh, Al Olaya",
        ApplicationNo: p.ApplicationNo,
        ContractNo: $"C-{9100 + index}",
        RelativeName: "Meron Tesfaye",
        RelativePhone: "+251911999888",
        RelativeKinship: "Sister");
}
