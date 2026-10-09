using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Asking what the enjaze form is missing, then filling it in.
///
/// Printing used to refuse a batch and name the candidates with gaps, which left the desk to go
/// and find each one. The same answer is now fetched before the print and turned into boxes, so
/// what these pin down is that the answer is per candidate, machine-readable, and that saving it
/// actually closes the gap.
/// </summary>
public class VisaFormGapsTests : IDisposable
{
    private readonly TenantDbContext _context;

    public VisaFormGapsTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
    }

    private Candidate Given(string first, string last, Action<Candidate>? fill = null)
    {
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            PassportNumber = "EP4410233",
            DateOfBirth = new DateOnly(1998, 4, 12),
            VisaNumber = "V-7101",
            ENumber = "E826000111",
            SponsorName = "Example Sponsor",
            SponsorIdNumber = "1040000001",
            PassportIssueDate = new DateOnly(2024, 6, 8),
            PassportExpiryDate = new DateOnly(2029, 6, 7),
        };
        fill?.Invoke(candidate);
        _context.Candidates.Add(candidate);
        _context.SaveChanges();
        return candidate;
    }

    private Task<SimbaFlow.Application.Common.Models.Result<List<VisaFormGapDto>>> Gaps(
        params Guid[] ids) =>
        new GetVisaFormGapsHandler(_context).Handle(new GetVisaFormGapsQuery([.. ids]), default);

    [Fact]
    public async Task ACandidateWithNothingMissingHasNoGaps()
    {
        var ready = Given("Almaz", "Kebede");

        var result = await Gaps(ready.Id);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Should().ContainSingle().Which.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task EachCandidateIsAskedOnlyForWhatTheyAreMissing()
    {
        // The reason this is per candidate rather than one shared form: two people in the same
        // batch are rarely missing the same thing, and asking everyone for everything means
        // retyping what is already on file.
        var noENumber = Given("Almaz", "Kebede", c => c.ENumber = null);
        var noSponsor = Given("Hanan", "Ahmed", c =>
        {
            c.SponsorName = null;
            c.SponsorIdNumber = null;
        });

        var result = await Gaps(noENumber.Id, noSponsor.Id);

        var byName = result.Data!.ToDictionary(g => g.FullName);
        byName["Almaz Kebede"].Fields.Select(f => f.Key).Should().Equal("eNumber");
        byName["Hanan Ahmed"].Fields.Select(f => f.Key)
            .Should().Equal("sponsorName", "sponsorIdNumber");
    }

    [Fact]
    public async Task EveryGapCarriesBothALabelAndTheKeyThatSavesIt()
    {
        // A label the desk reads and a key the save understands, from one list — a box captioned
        // "Passport date of issue" that posts nothing the server recognises saves no one.
        var candidate = Given("Almaz", "Kebede", c => c.PassportIssueDate = null);

        var field = (await Gaps(candidate.Id)).Data!.Single().Fields.Single();

        field.Key.Should().Be("passportIssueDate");
        field.Label.Should().Be("Passport date of issue");
    }

    [Fact]
    public async Task FillingTheGapsInClosesThem()
    {
        var candidate = Given("Almaz", "Kebede", c =>
        {
            c.ENumber = null;
            c.PassportExpiryDate = null;
        });

        await new SetVisaDetailsHandler(_context).Handle(
            new SetVisaDetailsCommand(
                candidate.Id, ENumber: "E826000222", PassportExpiryDate: "2029-06-07"),
            default);

        (await Gaps(candidate.Id)).Data!.Single().Fields.Should().BeEmpty();
        VisaFormReadiness.IsReady(candidate).Should().BeTrue();
    }

    [Fact]
    public async Task AnUnreadableDateLeavesWhatIsOnFileRatherThanClearingIt()
    {
        // This command exists to fill gaps, so it must never be able to open one.
        var candidate = Given("Almaz", "Kebede");

        await new SetVisaDetailsHandler(_context).Handle(
            new SetVisaDetailsCommand(candidate.Id, PassportExpiryDate: "not a date"), default);

        candidate.PassportExpiryDate.Should().Be(new DateOnly(2029, 6, 7));
    }

    [Fact]
    public async Task AskingAboutNobodyIsRefusedRatherThanAnsweredWithNothing()
    {
        (await Gaps()).IsSuccess.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
