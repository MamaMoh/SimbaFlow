using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services.Reporting;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The Tasheer submission sheet.
///
/// This file is read by another system, not by a person, so what it is tested against is that
/// system's template: the headings it expects, in its order, with nothing above them. Everything
/// here is invented.
/// </summary>
public class TasheerSheetTests : IDisposable
{
    private static readonly string[] Template =
    [
        "E.No", "First Name*", "Second Name", "Last Name*", "Passport Number*", "Date of Birth*",
        "Nationality*", "Date of Issue*", "Gender*", "Place of Issue*", "Expiry Date*",
        "Applicant Mobile No.*", "Email ID*",
    ];

    private readonly TenantDbContext _context;
    private readonly ExportTasheerListHandler _handler;

    public TasheerSheetTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        var branding = Substitute.For<IDocumentBrandingService>();
        branding.GetAgencyIdentityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgencyIdentity(
                "SAMPLE FOREIGN EMPLOYMENT AGENCY PLC", "office@example.com", null, "LB/0001")));

        _handler = new ExportTasheerListHandler(_context, new ReportExportService(), branding);
    }

    private Guid Given(Action<Candidate>? tweak = null)
    {
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "ALMAZ",
            MiddleName = "DEBELA",
            LastName = "KEBEDE",
            PassportNumber = "EP0000001",
            PassportIssueDate = new DateOnly(2024, 6, 8),
            PassportExpiryDate = new DateOnly(2029, 6, 7),
            PassportPlaceOfIssue = "Ethiopia",
            DateOfBirth = new DateOnly(2002, 7, 9),
            Nationality = "Ethiopia",
            Gender = Gender.Female,
            PhoneNumber = "+251911000000",
            ENumber = "E00111222",
        };
        tweak?.Invoke(candidate);
        _context.Candidates.Add(candidate);
        _context.SaveChanges();
        return candidate.Id;
    }

    private async Task<IXLWorksheet> SheetFor(params Guid[] ids)
    {
        var result = await _handler.Handle(new ExportTasheerListCommand([.. ids]), default);
        result.IsSuccess.Should().BeTrue();

        var workbook = new XLWorkbook(new MemoryStream(result.Data!));
        return workbook.Worksheet(1);
    }

    private static IReadOnlyList<string> RowText(IXLWorksheet sheet, int row) =>
        [.. Enumerable.Range(1, Template.Length).Select(c => sheet.Cell(row, c).GetString())];

    [Fact]
    public async Task TheHeadingsAreTheTemplatesHeadingsOnTheFirstRow()
    {
        // Row one, asterisks and all. A title above them makes every column unrecognised, and an
        // importer matching on text does not know "First Name" is "First Name*".
        var sheet = await SheetFor(Given());

        RowText(sheet, 1).Should().Equal(Template);
    }

    [Fact]
    public async Task ACandidateIsOneRowUnderThem()
    {
        var sheet = await SheetFor(Given());

        RowText(sheet, 2).Should().Equal(
            "E00111222", "ALMAZ", "DEBELA", "KEBEDE", "EP0000001", "2002-07-09", "Ethiopia",
            "2024-06-08", "Female", "ETHIOPIA", "2029-06-07", "+251911000000", "office@example.com");
    }

    [Fact]
    public async Task DatesAreUnambiguous()
    {
        // dd/MM and MM/dd are the same eleven days a month written the other way round, and a
        // reader that guesses wrong turns the 7th of September into the 9th of July in silence.
        var sheet = await SheetFor(Given(c =>
        {
            c.DateOfBirth = new DateOnly(2002, 9, 7);
            c.PassportIssueDate = new DateOnly(2024, 1, 2);
        }));

        sheet.Cell(2, 6).GetString().Should().Be("2002-09-07");
        sheet.Cell(2, 8).GetString().Should().Be("2024-01-02");
    }

    [Fact]
    public async Task ANameTypedIntoTooFewBoxesIsStillSplitCorrectly()
    {
        // Registered with two names in the first box. Taken from the stored fields, the father's
        // name would go out in the first-name column.
        var sheet = await SheetFor(Given(c =>
        {
            c.FirstName = "ALMAZ DEBELA";
            c.MiddleName = null;
            c.LastName = "KEBEDE";
        }));

        RowText(sheet, 2).Take(4).Should().Equal("E00111222", "ALMAZ", "DEBELA", "KEBEDE");
    }

    [Fact]
    public async Task TheAgencysAddressStandsInForACandidateWithNoEmail()
    {
        // Email ID is required and candidates do not have mailboxes; Tasheer writes to the
        // agency about the batch. A candidate's own address wins where there is one.
        RowText(await SheetFor(Given()), 2)[^1].Should().Be("office@example.com");

        var withOwn = Given(c => c.Email = "almaz@example.com");
        RowText(await SheetFor(withOwn), 2)[^1].Should().Be("almaz@example.com");
    }

    [Fact]
    public async Task EveryoneSelectedIsOnTheSheet()
    {
        var sheet = await SheetFor(
            Given(c => c.LastName = "ABDI"),
            Given(c => c.LastName = "KEBEDE"),
            Given(c => c.LastName = "MEKONNEN"));

        sheet.LastRowUsed()!.RowNumber().Should().Be(4, "one heading row and three candidates");
        RowText(sheet, 2)[3].Should().Be("ABDI", "the sheet is ordered the way a person files it");
    }

    [Fact]
    public async Task NothingIsAddedForAReader()
    {
        // No running number down the side, no filter dropdowns, nothing merged. Anything extra
        // is a column the importer did not ask for.
        var sheet = await SheetFor(Given());

        sheet.Cell(1, Template.Length + 1).GetString().Should().BeEmpty();
        sheet.MergedRanges.Should().BeEmpty();
        sheet.AutoFilter.IsEnabled.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
