using FluentAssertions;
using MediatR;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.Application.Common.Behaviors;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Who opened which candidate. The read-audit service and its batch writer were built and
/// registered, but nothing ever called them, so the ReadAuditLogs table stayed empty however many
/// passports, CVs and contracts were viewed or printed.
/// </summary>
public class ReadAuditBehaviorTests
{
    private readonly IReadAuditService _readAudit = Substitute.For<IReadAuditService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    public ReadAuditBehaviorTests()
    {
        _currentUser.UserId.Returns("user-1");
        _currentUser.IpAddress.Returns("10.0.0.7");
        _currentUser.UserAgent.Returns("browser");
    }

    private Task<TResponse> Run<TRequest, TResponse>(TRequest request, TResponse response)
        where TRequest : notnull
    {
        var behavior = new ReadAuditBehavior<TRequest, TResponse>(_readAudit, _currentUser);
        return behavior.Handle(request, _ => Task.FromResult(response), CancellationToken.None);
    }

    [Fact]
    public async Task OpeningACandidate_LogsWhoReadIt()
    {
        var id = Guid.NewGuid();

        await Run(new GetCandidateByIdQuery(id), Result<CandidateDetailDto>.Success(null!));

        _readAudit.Received(1).LogAccess(new ReadAuditEntry(
            "user-1", "Candidate", id.ToString(), nameof(GetCandidateByIdQuery),
            "10.0.0.7", "browser"));
    }

    [Fact]
    public async Task ABulkPrint_LogsEachCandidateOnce()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await Run(new GenerateBulkCvCommand([a, b, a]), Result<byte[]>.Success([]));

        _readAudit.Received(2).LogAccess(Arg.Any<ReadAuditEntry>());
        _readAudit.Received(1).LogAccess(Arg.Is<ReadAuditEntry>(e => e.EntityId == a.ToString()));
        _readAudit.Received(1).LogAccess(Arg.Is<ReadAuditEntry>(e => e.EntityId == b.ToString()));
    }

    [Fact]
    public async Task ARead_RecordsTheAgencyItHappenedIn()
    {
        var tenant = Guid.NewGuid();
        _currentUser.TenantId.Returns(tenant);

        await Run(new GetCandidateByIdQuery(Guid.NewGuid()), Result<CandidateDetailDto>.Success(null!));

        _readAudit.Received(1).LogAccess(Arg.Is<ReadAuditEntry>(e => e.TenantId == tenant));
    }

    [Fact]
    public async Task TheCandidateList_LogsOnceWithoutAnId()
    {
        await Run(
            new GetCandidatesQuery(1, 20, "abebe", null, null),
            Result<PaginatedCandidateResult>.Success(new([], 0, 1, 20, 0)));

        _readAudit.Received(1).LogAccess(Arg.Is<ReadAuditEntry>(e =>
            e.EntityId == null && e.Action == nameof(GetCandidatesQuery)));
    }

    [Fact]
    public async Task AFailedRead_IsNotLogged()
    {
        await Run(
            new GetCandidateMediaQuery(Guid.NewGuid(), "passport"),
            Result<CandidateMediaDto>.Failure("Candidate not found", 404));

        _readAudit.DidNotReceive().LogAccess(Arg.Any<ReadAuditEntry>());
    }

    [Fact]
    public async Task AThrowingRead_IsNotLoggedAndStillThrows()
    {
        var behavior = new ReadAuditBehavior<GenerateCVCommand, Result<GeneratedPdf>>(_readAudit, _currentUser);

        var act = () => behavior.Handle(
            new GenerateCVCommand(Guid.NewGuid()),
            _ => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _readAudit.DidNotReceive().LogAccess(Arg.Any<ReadAuditEntry>());
    }

    [Fact]
    public async Task ARequestThatIsNotARead_IsNotLogged()
    {
        await Run(new PlainQuery(), Result<string>.Success("ok"));

        _readAudit.DidNotReceive().LogAccess(Arg.Any<ReadAuditEntry>());
    }

    [Fact]
    public async Task TheResponseIsPassedThroughUnchanged()
    {
        var response = Result<CandidateDetailDto>.Success(null!);

        var returned = await Run(new GetCandidateByIdQuery(Guid.NewGuid()), response);

        returned.Should().BeSameAs(response);
    }

    /// <summary>
    /// A new endpoint that shows candidate data has to opt in, or it reads unaudited. Every
    /// candidate request gated on candidate.read is audited unless it is named here with a reason.
    /// </summary>
    [Fact]
    public void EveryCandidateReadIsAudited()
    {
        var notPersonalData = new HashSet<string>
        {
            nameof(GetCandidateFilterOptionsQuery), // counts per stage, country and partner
            nameof(GetVisaFormGapsQuery),           // which form fields are blank, before printing
            nameof(ReadContractCommand),            // parses an uploaded file, no candidate yet
        };

        var offenders = typeof(GetCandidateByIdQuery).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("SimbaFlow.API.Features.Candidates") == true)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Where(t => typeof(IRequirePermission).IsAssignableFrom(t))
            .Where(t => t.GetProperty(nameof(IRequirePermission.RequiredPermission))?.GetValue(
                System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t)) as string == "candidate.read")
            .Where(t => !typeof(IAuditedRead).IsAssignableFrom(t) && !notPersonalData.Contains(t.Name))
            .Select(t => t.Name)
            .ToList();

        offenders.Should().BeEmpty("a request that shows candidate data must say whose it read");
        typeof(IAuditedRead).IsAssignableFrom(typeof(GetCandidateTimelineQuery)).Should().BeTrue();
    }

    [Fact]
    public void TheBehaviorIsInThePipeline()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Directory.GetParent(dir)?.FullName;
        var source = File.ReadAllText(Path.Combine(
            dir!, "src", "SimbaFlow.API", "Extensions", "ServiceExtensions.cs"));

        source.Should().Contain("typeof(ReadAuditBehavior<,>)",
            "without the registration no read is ever audited, which is how this shipped");
    }

    private record PlainQuery : IRequest<Result<string>>;
}
