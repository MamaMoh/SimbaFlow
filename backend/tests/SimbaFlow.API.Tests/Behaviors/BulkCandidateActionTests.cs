using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.API.Features.Workflow;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// One board action for many candidates. Each candidate goes through the row's own command, so
/// these pin how the batch is formed and how each candidate's answer comes back — not the rules,
/// which belong to the commands and are tested there.
/// </summary>
public class BulkCandidateActionTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ITenantDbContext _context = Substitute.For<ITenantDbContext>();

    private Task<Result<BulkActionResult>> Run(IEnumerable<Guid> ids) =>
        BulkCandidateAction.RunAsync(ids, id => new BookMedicalCommand(id), _sender, _context);

    private void Answer(Guid id, Result result) =>
        _sender.Send(Arg.Is<BookMedicalCommand>(c => c.CandidateId == id), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task EachCandidateIsReportedOnItsOwn()
    {
        var hanan = Guid.NewGuid();
        var seada = Guid.NewGuid();
        Answer(hanan, Result.Success());
        Answer(seada, Result.Failure("Cannot book medical from status 'Fit'. Expected Pending or Unfit.", 400));

        var result = await Run([hanan, seada]);

        // A refusal is one candidate's answer, not a failure of the batch: the rest still happen.
        result.IsSuccess.Should().BeTrue();
        result.Data!.Succeeded.Should().Equal(hanan);
        result.Data.Failed.Should().ContainSingle()
            .Which.Should().Be(new BulkActionFailure(seada, "Cannot book medical from status 'Fit'. Expected Pending or Unfit."));
    }

    [Fact]
    public async Task EveryCandidateGoesThroughTheRowsOwnCommand()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids) Answer(id, Result.Success());

        await Run(ids);

        await _sender.Received(3).Send(Arg.Any<BookMedicalCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ACandidateTickedTwiceIsActedOnOnce()
    {
        var id = Guid.NewGuid();
        Answer(id, Result.Success());

        var result = await Run([id, id, Guid.Empty]);

        result.Data!.Succeeded.Should().Equal(id);
        await _sender.Received(1).Send(Arg.Any<BookMedicalCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NothingSelectedIsSaidPlainly()
    {
        var result = await Run([]);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ABatchLargerThanTheCapIsRefusedBeforeAnythingChanges()
    {
        var result = await Run(Enumerable.Range(0, BulkCandidateAction.MaxCandidates + 1).Select(_ => Guid.NewGuid()));

        result.StatusCode.Should().Be(400);
        await _sender.DidNotReceive().Send(Arg.Any<BookMedicalCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoPermissionIsOneAnswer_NotOnePerCandidate()
    {
        var first = Guid.NewGuid();
        Answer(first, Result.Failure("You do not have permission to do this.", 403));

        var result = await Run([first, Guid.NewGuid(), Guid.NewGuid()]);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        await _sender.Received(1).Send(Arg.Any<BookMedicalCommand>(), Arg.Any<CancellationToken>());
    }
}
