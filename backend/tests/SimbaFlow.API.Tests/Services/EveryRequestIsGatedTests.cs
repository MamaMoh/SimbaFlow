using FluentAssertions;
using MediatR;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.API.Features.SampleData;
using SimbaFlow.API.Features.Tenants.Commands;
using SimbaFlow.API.Features.Tenants.Queries;
using SimbaFlow.Application.Common.Interfaces;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Every command and query says who may send it.
///
/// The endpoints all sit behind RequireAuthorization(), which only asks whether somebody is signed
/// in. What a role may actually do is decided by the gate on the request itself, so a request
/// with no gate is open to every account in every agency — which is how the candidate timeline
/// shipped. This fails the build for the next one instead of waiting for someone to notice.
/// </summary>
public class EveryRequestIsGatedTests
{
    private static readonly Type[] Gates =
    [
        typeof(IRequirePermission),
        typeof(IRequireAnyPermission),
        typeof(IRequirePlatformAdmin),
        typeof(IRequirePlatformStaff),
        typeof(IRequireAgencyOwner),
    ];

    /// <summary>
    /// Requests about the caller's own account. Signing in, out, and changing your own password or
    /// second factor cannot ask for a permission: they are how you get one.
    /// </summary>
    private const string SelfServiceNamespace = "SimbaFlow.API.Features.Auth";

    [Fact]
    public void EveryCommandAndQueryCarriesAGate()
    {
        var requests = typeof(GetCandidatesQuery).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(i =>
                i == typeof(IRequest) ||
                (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))))
            .ToList();

        requests.Should().NotBeEmpty();

        var ungated = requests
            .Where(t => t.Namespace?.StartsWith(SelfServiceNamespace, StringComparison.Ordinal) != true)
            .Where(t => !Gates.Any(g => g.IsAssignableFrom(t)))
            .Select(t => t.FullName)
            .ToList();

        ungated.Should().BeEmpty(
            "a request with no permission, owner or platform gate can be sent by any signed-in account");
    }

    [Fact]
    public void TheCandidateTimelineNeedsCandidateRead()
    {
        IRequirePermission query = new GetCandidateTimelineQuery(Guid.NewGuid());
        query.RequiredPermission.Should().Be("candidate.read");
    }

    [Fact]
    public void TheAgencyListIsOpenToTheWholePlatformTeam()
    {
        // The support desk needs to see which agencies are struggling, so the list is staff-wide;
        // changing an agency stays with the platform administrators below.
        typeof(IRequirePlatformStaff).IsAssignableFrom(typeof(GetTenantsQuery)).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(ProvisionTenantCommand))]
    [InlineData(typeof(UpdateTenantStatusCommand))]
    public void AgencyAdministrationIsThePlatformTeams(Type request)
    {
        typeof(IRequirePlatformAdmin).IsAssignableFrom(request).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(SeedSampleDataCommand))]
    [InlineData(typeof(RemoveSampleDataCommand))]
    public void SampleDataIsTheOwnersCall(Type request)
    {
        typeof(IRequireAgencyOwner).IsAssignableFrom(request).Should().BeTrue();
    }
}
