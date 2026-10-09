using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Where a deleted candidate goes.
///
/// It used to be nowhere: the list filtered them out, so a colleague who went looking could not
/// tell a record somebody had removed from one that was never registered. They belong on the
/// Inactive tab with the name of whoever removed them — and, just as firmly, nowhere near the
/// default list, which is the one people work from.
/// </summary>
public class InactiveCandidateListTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly GetCandidatesHandler _handler;

    public InactiveCandidateListTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
        _handler = new GetCandidatesHandler(_context);
    }

    private void Given(string first, string last, bool deleted = false, string? by = null)
    {
        _context.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            PassportNumber = $"EP{Random.Shared.Next(1000000, 9999999)}",
            DateOfBirth = new DateOnly(1998, 5, 20),
            IsDeleted = deleted,
            Status = deleted ? CandidateStatus.Archived : CandidateStatus.Active,
            DeletedAt = deleted ? DateTime.UtcNow : null,
            DeletedBy = by,
        });
        _context.SaveChanges();
    }

    private async Task<List<CandidateListDto>> List(string? status)
    {
        var result = await _handler.Handle(
            new GetCandidatesQuery(1, 50, null, null, null, status), default);
        result.IsSuccess.Should().BeTrue();
        return result.Data!.Items;
    }

    [Fact]
    public async Task TheDefaultListLeavesDeletedCandidatesOut()
    {
        Given("Almaz", "Kebede");
        Given("Hanan", "Ahmed", deleted: true, by: "intake.clerk");

        (await List(null)).Should().ContainSingle().Which.FullName.Should().Be("Almaz Kebede");
    }

    [Fact]
    public async Task TheInactiveListShowsThemWithWhoRemovedThem()
    {
        Given("Almaz", "Kebede");
        Given("Hanan", "Ahmed", deleted: true, by: "intake.clerk");

        var inactive = await List("inactive");

        inactive.Should().ContainSingle();
        inactive[0].FullName.Should().Be("Hanan Ahmed");
        inactive[0].DeletedBy.Should().Be("intake.clerk");
        inactive[0].DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AWithdrawnCandidateIsInactiveToo()
    {
        // Inactive is not only "deleted" — someone withdrawn from the pipeline, medically unfit
        // for instance, is off the active list without anyone having removed their record.
        _context.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "Meron",
            LastName = "Tesfaye",
            PassportNumber = "EP4410999",
            DateOfBirth = new DateOnly(1999, 1, 1),
            Status = CandidateStatus.Inactive,
        });
        _context.SaveChanges();

        (await List("inactive")).Should().ContainSingle()
            .Which.DeletedBy.Should().BeNull("nobody deleted them");
    }

    [Fact]
    public async Task AllMeansAll()
    {
        Given("Almaz", "Kebede");
        Given("Hanan", "Ahmed", deleted: true, by: "intake.clerk");

        (await List("all")).Should().HaveCount(2);
    }

    public void Dispose() => _context.Dispose();
}
