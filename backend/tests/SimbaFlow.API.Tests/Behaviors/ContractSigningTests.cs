using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The signing date, which is no longer typed.
///
/// It was a date box two fields away from a Contract date box that means the same thing, so one
/// got filled in and the other did not, and which one varied by who was at the desk.
/// </summary>
public class ContractSigningTests
{
    private static readonly DateTime Registered = new(2026, 10, 10, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void TheContractsOwnDateIsTheSigningDate()
    {
        ContractSigning.Date(new DateOnly(2026, 9, 1), Registered)
            .Should().Be(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void WithoutAContractItIsTheDayTheRecordWasOpened()
    {
        ContractSigning.Date(null, Registered).Should().Be(new DateOnly(2026, 10, 10));
    }
}
