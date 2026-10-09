using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Finding a candidate in the list.
///
/// The search used to run in the browser over whichever page of rows the table was holding, so a
/// desk with two thousand people on the books searched a hundred of them and concluded the
/// candidate was not there. It runs in the database now, which is what these pin down — along
/// with the age range, which is asked for in years and stored as a date of birth.
///
/// In-memory provider, so ILike falls back to a case-sensitive Contains; the cases below are
/// chosen to be meaningful either way.
/// </summary>
public class CandidateSearchTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly GetCandidatesHandler _handler;

    public CandidateSearchTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        _handler = new GetCandidatesHandler(_context);
    }

    private void Given(
        string first, string middle, string last,
        string passport = "EP0000001",
        int age = 25,
        string? phone = null,
        string? eNumber = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _context.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            MiddleName = middle,
            LastName = last,
            PassportNumber = passport,
            PhoneNumber = phone,
            ENumber = eNumber,
            // Mid-year birthday so adding whole years cannot land on today and tip the age over.
            DateOfBirth = today.AddYears(-age).AddDays(-30),
        });
        _context.SaveChanges();
    }

    private async Task<List<string>> Find(
        string? search = null, int? minAge = null, int? maxAge = null)
    {
        var result = await _handler.Handle(
            new GetCandidatesQuery(1, 50, search, null, null, null, minAge, maxAge), default);

        result.IsSuccess.Should().BeTrue();
        return [.. result.Data!.Items.Select(i => i.FullName)];
    }

    [Fact]
    public async Task AFullNameIsFoundEvenThoughItIsStoredInThreeColumns()
    {
        // The one that sent people looking in the database. "Almaz Kebede" is in neither the
        // first-name column nor the last-name column, so matching them one at a time found
        // nothing at all.
        Given("Almaz", "Debela", "Kebede");

        (await Find("Almaz Kebede")).Should().ContainSingle();
        (await Find("Almaz Debela Kebede")).Should().ContainSingle();
        (await Find("Debela")).Should().ContainSingle();
    }

    [Fact]
    public async Task APhoneNumberFindsThem()
    {
        // The desk is often holding a phone that rang rather than a name.
        Given("Almaz", "Debela", "Kebede", phone: "+251911000000");

        (await Find("911000000")).Should().ContainSingle();
    }

    [Fact]
    public async Task AnENumberFindsThem()
    {
        Given("Almaz", "Debela", "Kebede", eNumber: "E000111222");

        (await Find("E000111222")).Should().ContainSingle();
    }

    [Fact]
    public async Task SomebodyElseIsNotReturned()
    {
        Given("Almaz", "Debela", "Kebede", passport: "EP0000001");
        Given("Hanan", "Abdi", "Nuru", passport: "EP0000002");

        (await Find("Hanan")).Should().Equal("Hanan Abdi Nuru");
        (await Find("EP0000001")).Should().Equal("Almaz Debela Kebede");
    }

    [Fact]
    public async Task TheAgeRangeIncludesBothEnds()
    {
        // Someone is 25 from their 25th birthday until the day before their 26th, so a 25–30
        // range has to hold both a 25-year-old and a 30-year-old.
        Given("Young", "", "Twentyfour", age: 24);
        Given("Lower", "", "Twentyfive", age: 25);
        Given("Upper", "", "Thirty", age: 30);
        Given("Older", "", "Thirtyone", age: 31);

        var found = await Find(minAge: 25, maxAge: 30);

        found.Should().BeEquivalentTo("Lower Twentyfive", "Upper Thirty");
    }

    [Fact]
    public async Task EitherEndOfTheRangeWorksOnItsOwn()
    {
        Given("Young", "", "Twenty", age: 20);
        Given("Older", "", "Forty", age: 40);

        (await Find(minAge: 30)).Should().Equal("Older Forty");
        (await Find(maxAge: 30)).Should().Equal("Young Twenty");
    }

    [Fact]
    public async Task TheRowCarriesWhatTheListNowShows()
    {
        Given("Almaz", "Debela", "Kebede", phone: "+251911000000");
        var candidate = await _context.Candidates.FirstAsync();
        candidate.ExperienceAbroadYears = 3;
        candidate.WorksIn = "United Arab Emirates";
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetCandidatesQuery(1, 50, null, null, null), default);
        var row = result.Data!.Items.Single();

        row.PhoneNumber.Should().Be("+251911000000");
        row.ExperienceAbroadYears.Should().Be(3);
        row.WorksIn.Should().Be("United Arab Emirates");
        row.Age.Should().Be(25);
    }

    [Fact]
    public async Task TheFilterOptionsAreCountedOverEveryCandidateNotOverOnePage()
    {
        // The stage chips used to be built from the rows the browser was holding, so an agency
        // with more candidates than one page saw counts that were a sample of themselves.
        for (var i = 0; i < 30; i++) Given("Almaz", "Debela", $"Kebede{i}", $"EP000{i:D4}");

        var options = await new GetCandidateFilterOptionsHandler(_context)
            .Handle(new GetCandidateFilterOptionsQuery(), default);

        options.IsSuccess.Should().BeTrue();
        options.Data!.Total.Should().Be(30);
        options.Data.Stages.Sum(x => x.Count).Should().Be(30);
    }

    public void Dispose() => _context.Dispose();
}
