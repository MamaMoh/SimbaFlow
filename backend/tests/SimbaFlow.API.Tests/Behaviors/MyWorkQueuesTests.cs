using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Tasks;
using SimbaFlow.API.Features.Tasks.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// My work: a tab per board the role can open, and on each tab who is waiting on this desk.
///
/// What these pin down is the promise made to the desk: an embassy officer sees Embassy and
/// nothing else, somebody holding embassy and travel gets both to switch between, and each row
/// says whether it is theirs to do now or somebody else's to finish.
/// </summary>
public class MyWorkQueuesTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly WorkflowStage _embassy = Stage("Embassy");
    private readonly WorkflowStage _lmis = Stage("LMIS");
    private readonly WorkflowStage _ticket = Stage("Ticket");
    private readonly WorkflowStage _departure = Stage("Departure");
    private readonly WorkflowStage _arrival = Stage("Arrival");
    private readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    public MyWorkQueuesTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        _context.WorkflowStages.AddRange(_embassy, _lmis, _ticket, _departure, _arrival);
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static WorkflowStage Stage(string name) =>
        new() { Id = Guid.NewGuid(), WorkflowDefinitionId = Guid.NewGuid(), Name = name };

    private void Holding(params string[] permissions) =>
        _user.HasPermission(Arg.Any<string>()).Returns(ci => permissions.Contains(ci.Arg<string>()));

    private Candidate Given(
        string name, WorkflowStage stage, object? status = null, Action<Candidate>? fill = null)
    {
        var parts = name.Split(' ');
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = parts[0],
            LastName = parts[1],
            PassportNumber = "EP" + Random.Shared.Next(1000000, 9999999),
            CurrentStageId = stage.Id,
            CurrentStageName = stage.Name,
            StageEnteredAt = DateTime.UtcNow.AddDays(-2),
            CurrentStatusValues = JsonDocument.Parse(JsonSerializer.Serialize(status ?? new { })),
        };
        fill?.Invoke(candidate);
        _context.Candidates.Add(candidate);
        _context.SaveChanges();
        return candidate;
    }

    private async Task<MyWorkQueuesResult> Queues(GetMyWorkQueuesQuery? query = null)
    {
        var result = await new GetMyWorkQueuesHandler(_context, _user)
            .Handle(query ?? new GetMyWorkQueuesQuery(), default);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Data!;
    }

    [Fact]
    public async Task AnEmbassyOfficerSeesOnlyTheEmbassyTab()
    {
        Holding("embassy.read", "candidate.read");

        var queues = await Queues();

        queues.Queues.Select(q => q.Key).Should().Equal("embassy");
    }

    [Fact]
    public async Task SomebodyWithEmbassyAndTravelSwitchesBetweenThem()
    {
        Holding("embassy.read", "travel.read");

        var queues = await Queues();

        queues.Queues.Select(q => q.Key).Should().Equal("embassy", "tickets", "departures");
    }

    [Fact]
    public async Task AnAdminSeesEveryBoard()
    {
        Holding("system.admin");

        var queues = await Queues();

        // Intake, New Contracts and Case Executive are not set up in this tenant, so they are
        // left out rather than shown empty.
        queues.Queues.Select(q => q.Key).Should()
            .Equal("embassy", "lmis", "tickets", "departures", "arrivals", "exceptions", "commissions");
    }

    [Fact]
    public async Task RowsSayWhetherTheDeskOwesTheNextStep()
    {
        Holding("embassy.read");
        Given("Almaz Kebede", _embassy);
        Given("Hanan Ahmed", _embassy, new
        {
            medical = "Booked",
            medical_appointment_date = _today.AddDays(3).ToString("yyyy-MM-dd"),
            tasheer = "Book Done",
        });

        var embassy = (await Queues()).Queues.Single();

        embassy.NeedsActionCount.Should().Be(1);
        embassy.WaitingCount.Should().Be(1);
        embassy.Items[0].FullName.Should().Be("Almaz Kebede");
        embassy.Items[0].NextStep.Should().Be("Book medical · Book Tasheer");
        embassy.Items[1].NeedsAction.Should().BeFalse();
        embassy.Items[1].NextStep.Should().Be("Medical in 3 days");
    }

    [Fact]
    public async Task ACandidateMirroredIntoLmisIsOnBothBoards()
    {
        Holding("embassy.read", "lmis.read");
        Given("Almaz Kebede", _embassy, new { medical = "Fit", tasheer = "Book Done", visa = "Submitted" },
            c => c.VisibleInStages = [_lmis.Id]);

        var queues = (await Queues()).Queues;

        queues.Single(q => q.Key == "embassy").Items.Single().NeedsAction.Should().BeFalse();
        queues.Single(q => q.Key == "lmis").Items.Single().NextStep.Should().Be("Pay insurance");
    }

    [Fact]
    public async Task OverdueAndExpiredPassportsComeFirstWithinTheirPart()
    {
        Holding("travel.read");
        Given("Recent Booking", _ticket);
        Given("Old Booking", _ticket, fill: c => c.StageEnteredAt = DateTime.UtcNow.AddDays(-20));
        Given("Expired Passport", _ticket, fill: c => c.PassportExpiryDate = _today.AddDays(-1));

        var tickets = (await Queues()).Queues.Single(q => q.Key == "tickets");

        tickets.Items.Select(i => i.FullName).Should()
            .Equal("Old Booking", "Expired Passport", "Recent Booking");
        tickets.Items[0].Overdue.Should().BeTrue();
        tickets.OverdueCount.Should().Be(1);
        tickets.Items[1].Warning.Should().Be("Passport expired");
    }

    [Fact]
    public async Task FiltersNarrowEveryTabAndKeepTheirOwnOptions()
    {
        Holding("travel.read");
        var office = new Office { Id = Guid.NewGuid(), Name = "Dessie" };
        _context.Offices.Add(office);
        Given("Almaz Kebede", _ticket, fill: c => { c.PartnerName = "Gulf Star"; c.OfficeId = office.Id; });
        Given("Hanan Ahmed", _ticket, fill: c => c.PartnerName = "Riyadh Homes");

        var byPartner = await Queues(new GetMyWorkQueuesQuery(Partner: "gulf star"));
        var byOffice = await Queues(new GetMyWorkQueuesQuery(OfficeId: office.Id));
        var bySearch = await Queues(new GetMyWorkQueuesQuery(Search: "hanan"));

        byPartner.Queues.Single(q => q.Key == "tickets").Items.Single().FullName.Should().Be("Almaz Kebede");
        byPartner.Partners.Select(p => p.Value).Should().Equal("Gulf Star", "Riyadh Homes");
        byOffice.Queues.Single(q => q.Key == "tickets").Items.Single().OfficeName.Should().Be("Dessie");
        bySearch.Queues.Single(q => q.Key == "tickets").Items.Single().FullName.Should().Be("Hanan Ahmed");
    }

    [Fact]
    public async Task ARowOpensItsBoardOnThatCandidate()
    {
        Holding("travel.read");
        var c = Given("Almaz Kebede", _ticket);

        var row = (await Queues()).Queues.Single(q => q.Key == "tickets").Items.Single();

        row.Href.Should().Be($"/workflow/tickets?q={c.PassportNumber}");
    }

    [Fact]
    public async Task FinishedArrivalsLeaveTheArrivalsTab()
    {
        Holding("arrival.read");
        Given("Almaz Kebede", _arrival, new { arrival = "Arrived", commission_linked = "true" });
        Given("Hanan Ahmed", _arrival, new { arrival = "Arrived" });

        var queues = (await Queues()).Queues;

        queues.Single(q => q.Key == "arrivals").Items.Single().NextStep.Should().Be("Add to commission");
    }

    [Fact]
    public async Task OpenCasesAreListedOnTheExceptionsTabAndFlaggedElsewhere()
    {
        Holding("arrival.read", "travel.read");
        var c = Given("Almaz Kebede", _ticket);
        var exceptionCase = new ExceptionCase { Id = Guid.NewGuid(), CandidateId = c.Id, Type = ExceptionType.Returned };
        _context.ExceptionCases.Add(exceptionCase);
        _context.SaveChanges();

        var queues = (await Queues()).Queues;

        var row = queues.Single(q => q.Key == "exceptions").Items.Single();
        row.NextStep.Should().Be("Returned: start the investigation");
        row.Href.Should().Be($"/workflow/exceptions/{exceptionCase.Id}");
        queues.Single(q => q.Key == "tickets").Items.Single().Warning.Should().Be("Open exception case");
    }
}

public class WorkQueueStepsTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    private static Dictionary<string, string> S(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the step says and whose turn it is, which is what these tests are about.
    ///
    /// Not the whole record: a step also carries the tracks it belongs to, and those are lists,
    /// which a record compares by reference. Comparing whole steps passed only because every
    /// step had the same empty list.
    /// </summary>
    private static void Says(WorkQueueSteps.Step step, string text, bool needsAction)
    {
        step.Text.Should().Be(text);
        step.NeedsAction.Should().Be(needsAction);
    }

    [Theory]
    [InlineData("", "", "", "Book medical · Book Tasheer", true)]
    [InlineData("Fit", "Book Done", "", "Mark visa Ready", true)]
    [InlineData("Fit", "Book Done", "Ready", "Visa with Case Executive", false)]
    [InlineData("Fit", "Book Done", "Rejected", "Visa rejected: resubmit", true)]
    [InlineData("Fit", "Book Done", "Issued", "Send to LMIS", true)]
    [InlineData("Unfit", "Book Done", "", "Medical unfit: rebook or remove", true)]
    [InlineData("Fit", "Expired", "", "Tasheer expired: rebook", true)]
    public void EmbassyNamesWhatTheDeskOwes(string medical, string tasheer, string visa, string text, bool action)
    {
        var step = WorkQueueSteps.Embassy(S(("medical", medical), ("tasheer", tasheer), ("visa", visa)), Today);

        Says(step, text, action);
    }

    [Fact]
    public void ABookedAppointmentIsWaitingUntilItsDayThenNeedsTheResult()
    {
        var future = WorkQueueSteps.Embassy(
            S(("medical", "Fit"), ("tasheer", "Booked"), ("tasheer_appointment_date", "2026-10-17")), Today);
        var past = WorkQueueSteps.Embassy(
            S(("medical", "Fit"), ("tasheer", "Booked"), ("tasheer_appointment_date", "2026-10-15")), Today);

        Says(future, "Tasheer tomorrow", false);
        Says(past, "Record Tasheer result", true);
    }

    [Theory]
    [InlineData("Insurance Unpaid", "", "Pay insurance", true)]
    [InlineData("Available", "", "Upload to LMIS", true)]
    [InlineData("Available", "Uploaded", "Uploaded, waiting for the LMIS check", false)]
    [InlineData("Available", "Issued", "Send to Ticket", true)]
    public void LmisWaitsOnTheMinistryAfterTheUpload(string insurance, string milestone, string text, bool action)
    {
        Says(WorkQueueSteps.Lmis(S(("insurance", insurance), ("milestone", milestone))), text, action);
    }

    [Fact]
    public void TicketsAskForWhatTheMoveToDepartureNeeds()
    {
        WorkQueueSteps.Ticket(S()).Text.Should().Be("Book ticket");
        WorkQueueSteps.Ticket(S(("ticket_status", "Booking Complete"))).Text
            .Should().Be("Add destination and flight date");
        WorkQueueSteps.Ticket(S(("ticket_status", "Booking Complete"), ("destination", "Riyadh"), ("flight_date", "2026-10-20")))
            .Text.Should().Be("Send to Departure");
        WorkQueueSteps.Ticket(S(("canceled", "true"))).Text.Should().Be("Ticket canceled: rebook");
    }

    [Fact]
    public void ADepartureWaitsForTheFlightOnceTheCandidateIsTold()
    {
        Says(WorkQueueSteps.Departure(S(("flight_date", "2026-10-20")), Today),
            "Notify candidate, flies in 4 days", true);
        Says(WorkQueueSteps.Departure(S(("notification_status", "Notified"), ("flight_date", "2026-10-20")), Today),
            "Notified, flies in 4 days", false);
        Says(WorkQueueSteps.Departure(S(("notification_status", "Notified"), ("flight_date", "2026-10-16")), Today),
            "Confirm departure", true);
        WorkQueueSteps.Departure(S(("departure_status", "Not Departed")), Today).Text
            .Should().Be("Did not fly: send back to Ticket");
    }

    [Fact]
    public void ASubmittedVisaIsWaitingOnTheEmbassy()
    {
        Says(WorkQueueSteps.CaseExecutive(S(("visa", "Ready")), Today), "Submit visa documents", true);
        Says(WorkQueueSteps.CaseExecutive(S(("visa", "Submitted"), ("visa_submission_date", "2026-10-10")), Today),
            "Submitted 6 days ago, waiting for the embassy", false);
    }

    [Fact]
    public void ARowIsOnTheTrackOfWhatItIsWaitingFor_TEST92()
    {
        // A desk owing two things at once is on both: filtering Embassy by Medical and by
        // Tasheer both have to find this candidate.
        var both = WorkQueueSteps.Embassy(S(("medical", ""), ("tasheer", "")), Today);
        both.Tracks.Should().BeEquivalentTo([WorkQueueSteps.Tracks.Medical, WorkQueueSteps.Tracks.Tasheer]);

        var visa = WorkQueueSteps.Embassy(S(("medical", "Fit"), ("tasheer", "Book Done")), Today);
        visa.Tracks.Should().BeEquivalentTo([WorkQueueSteps.Tracks.Visa]);
    }

    [Fact]
    public void TheTracksAreWhatIsOwedNowAndNotWhatWasOwedBefore_TEST92()
    {
        // Medical and Tasheer are both done; the row is about the visa and nothing else, so
        // the medical filter must not keep showing it.
        var step = WorkQueueSteps.Embassy(
            S(("medical", "Fit"), ("tasheer", "Book Done"), ("visa", "Submitted")), Today);

        step.NeedsAction.Should().BeFalse();
        step.Tracks.Should().BeEquivalentTo([WorkQueueSteps.Tracks.Visa]);
    }

    [Fact]
    public void AWaitingAppointmentStaysOnItsOwnTrack_TEST92()
    {
        var step = WorkQueueSteps.Embassy(
            S(("medical", "Fit"), ("tasheer", "Booked"), ("tasheer_appointment_date", "2026-10-17")), Today);

        step.Tracks.Should().BeEquivalentTo([WorkQueueSteps.Tracks.Tasheer]);
    }

    [Fact]
    public void EveryBoardNamesATrackForItsStep_TEST92()
    {
        WorkQueueSteps.Intake().Tracks.Should().NotBeEmpty();
        WorkQueueSteps.NewContracts(S()).Tracks.Should().NotBeEmpty();
        WorkQueueSteps.CaseExecutive(S(), Today).Tracks.Should().NotBeEmpty();
        WorkQueueSteps.Lmis(S()).Tracks.Should().NotBeEmpty();
        WorkQueueSteps.Ticket(S()).Tracks.Should().NotBeEmpty();
        WorkQueueSteps.Departure(S(), Today).Tracks.Should().NotBeEmpty();
        WorkQueueSteps.Arrival(S()).Tracks.Should().NotBeEmpty();
    }

    [Fact]
    public void LmisSeparatesTheInsuranceFromTheMinistry_TEST92()
    {
        WorkQueueSteps.Lmis(S(("insurance", "Insurance Unpaid"))).Tracks
            .Should().BeEquivalentTo([WorkQueueSteps.Tracks.Insurance]);
        WorkQueueSteps.Lmis(S(("insurance", "Available"))).Tracks
            .Should().BeEquivalentTo([WorkQueueSteps.Tracks.Milestone]);
    }
}
