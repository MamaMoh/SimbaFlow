using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Offices;
using SimbaFlow.API.Features.Reports.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The Office comparison report groups by the agency's own branch.
///
/// It used to group by the foreign partner's name, so "office" in the report meant the company
/// abroad the candidate was sent to — two branches sending to the same partner showed as one line,
/// and one branch sending to three partners showed as three.
/// </summary>
public class OfficeComparisonReportTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly GetReportHandler _handler;

    public OfficeComparisonReportTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        _handler = new GetReportHandler(_context);
    }

    private Office GivenOffice(string name, bool active = true)
    {
        var office = new Office { Name = name, IsActive = active };
        _context.Offices.Add(office);
        _context.SaveChanges();
        return office;
    }

    private Candidate GivenCandidate(Office? office, string partner, decimal owed = 0m)
    {
        var candidate = new Candidate
        {
            FirstName = "Almaz",
            LastName = "Kebede",
            PassportNumber = $"EP{Random.Shared.Next(1000000, 9999999)}",
            DateOfBirth = new DateOnly(1998, 5, 20),
            Status = CandidateStatus.Active,
            PartnerName = partner,
            OfficeId = office?.Id,
        };
        _context.Candidates.Add(candidate);
        if (owed != 0m)
            _context.Commissions.Add(new Commission
            {
                CandidateId = candidate.Id,
                PartnerName = partner,
                TotalFeesAmount = owed,
                BalanceAmount = owed,
            });
        _context.SaveChanges();
        return candidate;
    }

    private async Task<List<Dictionary<string, object?>>> Rows()
    {
        var result = await _handler.Handle(new GetReportQuery("office-comparison"), default);
        result.IsSuccess.Should().BeTrue();
        return result.Data!.Rows;
    }

    [Fact]
    public async Task CandidatesAndCommissionAreCountedUnderTheOfficeNotThePartner()
    {
        var addis = GivenOffice("Addis Ababa");
        var dessie = GivenOffice("Dessie");
        GivenCandidate(addis, "Al Riyadh Manpower", owed: 1000m);
        GivenCandidate(addis, "Dubai Domestic", owed: 500m);
        GivenCandidate(dessie, "Al Riyadh Manpower", owed: 250m);

        var rows = await Rows();

        rows.Select(r => r["office"]).Should().Equal("Addis Ababa", "Dessie");
        rows[0]["candidates"].Should().Be(2);
        rows[0]["owed"].Should().Be(1500m);
        rows[1]["candidates"].Should().Be(1);
        rows[1]["owed"].Should().Be(250m);
    }

    [Fact]
    public async Task AnOpenOfficeWithNobodyOnItStillHasALine()
    {
        GivenCandidate(GivenOffice("Addis Ababa"), "Al Riyadh Manpower");
        GivenOffice("Hawassa");

        var rows = await Rows();

        rows.Should().Contain(r => (string)r["office"]! == "Hawassa" && (int)r["candidates"]! == 0);
    }

    [Fact]
    public async Task CandidatesWithNoOfficeAreListedLast()
    {
        var addis = GivenOffice("Addis Ababa");
        GivenCandidate(null, "Al Riyadh Manpower");
        GivenCandidate(null, "Al Riyadh Manpower");
        GivenCandidate(addis, "Al Riyadh Manpower");

        var rows = await Rows();

        rows.Select(r => r["office"]).Should().Equal("Addis Ababa", "No office");
        rows[1]["candidates"].Should().Be(2);
    }

    [Fact]
    public async Task AClosedOfficeKeepsItsCandidatesButIsMarked()
    {
        GivenCandidate(GivenOffice("Bahir Dar", active: false), "Al Riyadh Manpower");
        GivenOffice("Gondar", active: false);

        var rows = await Rows();

        rows.Select(r => r["office"]).Should().Equal("Bahir Dar (closed)");
    }

    [Fact]
    public async Task ASingleOpenOfficeIsUsedWhenNoneIsChosen()
    {
        var addis = GivenOffice("Addis Ababa");
        GivenOffice("Old branch", active: false);

        var outcome = await OfficeAssignment.ForNewCandidateAsync(_context, null, default);

        outcome.IsValid.Should().BeTrue();
        outcome.OfficeId.Should().Be(addis.Id);
    }

    [Fact]
    public async Task WithSeveralOfficesNoneIsGuessed()
    {
        GivenOffice("Addis Ababa");
        GivenOffice("Dessie");

        var outcome = await OfficeAssignment.ForNewCandidateAsync(_context, null, default);

        outcome.IsValid.Should().BeTrue();
        outcome.OfficeId.Should().BeNull();
    }

    [Fact]
    public async Task ANewCandidateCannotBePutOnAClosedOffice()
    {
        var closed = GivenOffice("Bahir Dar", active: false);

        var outcome = await OfficeAssignment.ForNewCandidateAsync(_context, closed.Id, default);

        outcome.IsValid.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
