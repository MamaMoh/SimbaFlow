using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Reading the Saudi standard employment contract.
///
/// The fixtures below are the real form's wording and line breaks with nothing real in them. Every
/// name, address, licence, national ID, telephone number, contract number and visa number is
/// invented. The contracts these were modelled on name a worker, her employer and both their
/// phone numbers, and this repository is public — so none of that is here.
/// </summary>
public class SaudiContractParserTests
{
    /// <summary>A household employs the worker: section A is the person.</summary>
    private const string IndividualEmployer = """
        STANDARD EMPLOYMENT
        CONTRACT FOR ETHIOPIA
        DOMESTIC WORKERS (DW)
        BOUND FOR THE KINGDOM OF
        SAUDI ARABIA
        CONTRACT # 2000111222
        VISA NUMBER # 1900111222
        This Contract is entered into on Monday,
        corresponding to (21/09/2026), by and
        between:
        A. Employer:
        Name: SAAD ABDULLAH MOHAMMED
        ALHARBI
        National ID Number: 1099887766
        Address
        Street: 4120, Alnakheel, 11564
        City: Riyadh
        Contact Numbers
        Mobile: 966500000000
        Telephone: 0
        Hereinafter called the Employer
        Represented in the Kingdom of Saudi
        Arabia by Saudi Recruiting Agency:
        Name: Example Resources Company
        License no: 512
        Telephone: +966920000000
        Address
        Street: Al Malaz
        City: Riyadh
        Email: info@example.com
        B. Domestic Service Worker:
        Name: SAMPLE CANDIDATE
        Position: House Maid
        """;

    /// <summary>A company employs the worker: section A is the agency, and there is no person.</summary>
    private const string CompanyEmployer = """
        STANDARD EMPLOYMENT
        CONTRACT FOR ETHIOPIA
        DOMESTIC WORKERS (DW)
        BOUND FOR THE KINGDOM OF
        SAUDI ARABIA
        CONTRACT # 2000333444
        VISA NUMBER # 1300333444
        This Contract is entered into on Sunday,
        corresponding to (26/07/2026), by and
        between:
        A. Hereinafter called the Employer
        Represented in the Kingdom of Saudi
        Arabia by Saudi Recruiting Agency:
        Name: Example Resources Company
        License no: 512
        Telephone: +966800000000
        Address
        Street: Al Malaz
        City: Riyadh
        Email: info@example.com
        B. Domestic Service Worker:
        Name: SAMPLE CANDIDATE
        Position: House Maid
        """;

    [Theory]
    [InlineData(IndividualEmployer, "2000111222", "1900111222")]
    [InlineData(CompanyEmployer, "2000333444", "1300333444")]
    public void TheContractAndVisaNumbersAreRead(string text, string contract, string visa)
    {
        var details = SaudiContractParser.Parse(text);

        details.ContractNumber.Should().Be(contract);
        details.VisaNumber.Should().Be(visa);
    }

    [Fact]
    public void AHouseholdEmployerIsTheSponsor()
    {
        var details = SaudiContractParser.Parse(IndividualEmployer);

        details.SponsorIsCompany.Should().BeFalse();
        details.SponsorName.Should().Be("SAAD ABDULLAH MOHAMMED ALHARBI");
        details.SponsorIdNumber.Should().Be("1099887766");
        details.SponsorPhone.Should().Be("966500000000");
        details.SponsorAddress.Should().Be("4120, Alnakheel, 11564, Riyadh");
    }

    [Fact]
    public void TheAgencyBelowAHouseholdEmployerIsNotMistakenForIt()
    {
        // Both blocks carry Name:, Telephone:, Street: and City:. Reading section A to its end
        // would file the agency's licence as the sponsor's national ID and its switchboard as
        // their mobile — plausible-looking values, all wrong.
        var details = SaudiContractParser.Parse(IndividualEmployer);

        details.SponsorName.Should().NotContain("Example Resources");
        details.SponsorIdNumber.Should().NotBe("512");
        details.SponsorPhone.Should().NotBe("+966920000000");
        details.SponsorAddress.Should().NotContain("Al Malaz");
    }

    [Fact]
    public void ACompanyEmployerIsTheSponsorAndItsLicenceIsTheIdentifier()
    {
        var details = SaudiContractParser.Parse(CompanyEmployer);

        details.SponsorIsCompany.Should().BeTrue();
        details.SponsorName.Should().Be("Example Resources Company");
        details.SponsorIdNumber.Should().Be("512");
        details.SponsorPhone.Should().Be("+966800000000");
        details.SponsorAddress.Should().Be("Al Malaz, Riyadh");
    }

    [Fact]
    public void AWorkerIsNeverReadAsTheSponsor()
    {
        // Section B carries a Name: too, and it is the one name on the form that must not end up
        // in the sponsor field.
        foreach (var text in new[] { IndividualEmployer, CompanyEmployer })
            SaudiContractParser.Parse(text).SponsorName.Should().NotBe("SAMPLE CANDIDATE");
    }

    [Fact]
    public void APlaceholderTelephoneIsNotReadAsANumber()
    {
        // The household form writes "0" where there is no landline.
        SaudiContractParser.Parse(IndividualEmployer).SponsorPhone.Should().NotBe("0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This is a shopping list, not a contract.")]
    public void SomethingThatIsNotAContractReadsAsNothing(string? text)
    {
        var details = SaudiContractParser.Parse(text);

        details.IsEmpty.Should().BeTrue();
    }
}
