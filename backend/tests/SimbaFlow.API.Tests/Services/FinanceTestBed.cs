using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// One agency's ledger with the system chart of accounts and a single commission, for tests
/// that follow money through fees, payments and the journal. USD converts at 55.
/// </summary>
internal sealed class FinanceTestBed
{
    public required TenantDbContext Db { get; init; }
    public required ICurrentUserService User { get; init; }
    public required Guid UserId { get; init; }
    public required Commission Commission { get; init; }
    public required IExchangeRateService Fx { get; init; }
    public required JournalPostingService Journal { get; init; }

    public static async Task<FinanceTestBed> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"ledger_{Guid.NewGuid()}")
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var userId = Guid.NewGuid();
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(userId.ToString());
        user.UserName.Returns("finance.tester");
        user.TenantId.Returns(Guid.NewGuid());

        var db = new TenantDbContext(options, user);
        var candidate = new Candidate
        {
            FirstName = "Abebe",
            LastName = "Kebede",
            PassportNumber = "EP1234567",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = Gender.Male,
            Status = CandidateStatus.Active,
            RegisteredAt = DateTime.UtcNow
        };
        db.Candidates.Add(candidate);
        var commission = new Commission
        {
            CandidateId = candidate.Id,
            PartnerName = "Riyadh Partner",
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId
        };
        db.Commissions.Add(commission);
        await db.SaveChangesAsync();
        await new FinanceSeedService(NullLogger<FinanceSeedService>.Instance).EnsureDefaultChartOfAccountsAsync(db);

        var fx = Substitute.For<IExchangeRateService>();
        fx.ResolveRateToEtbAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Rate(call.ArgAt<string>(0)));
        fx.ConvertToEtbAsync(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => Math.Round(call.ArgAt<decimal>(0) * Rate(call.ArgAt<string>(1)), 2));

        return new FinanceTestBed
        {
            Db = db,
            User = user,
            UserId = userId,
            Commission = commission,
            Fx = fx,
            Journal = new JournalPostingService(db, NullLogger<JournalPostingService>.Instance),
        };
    }

    private static decimal Rate(string currency) =>
        string.Equals(currency, "ETB", StringComparison.OrdinalIgnoreCase) ? 1m : 55m;

    public async Task SetFeesAsync(params (string Type, decimal Amount)[] fees)
    {
        var result = await new UpsertCommissionFeesHandler(Db, Fx, Journal, User).Handle(
            new UpsertCommissionFeesCommand(Commission.Id,
                fees.Select((f, i) => new FeeLineInput(f.Type, null, f.Amount, "ETB", i)).ToList()),
            CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    public Task<Application.Common.Models.Result<Guid>> TryPayAsync(decimal amount, string currency = "ETB") =>
        new RecordPaymentHandler(Db, Fx, Journal, User).Handle(
            new RecordPaymentCommand(Commission.Id, amount, currency, "Cash", null, null, null),
            CancellationToken.None);

    public async Task<Guid> PayAsync(decimal amount, string currency = "ETB")
    {
        var result = await TryPayAsync(amount, currency);
        result.IsSuccess.Should().BeTrue(result.Error);
        return result.Data;
    }

    /// <summary>Debit minus credit across every posted line on the account.</summary>
    public async Task<decimal> BalanceAsync(string code)
    {
        var lines = await Db.JournalLines
            .Where(l => !l.IsDeleted && !l.JournalEntry!.IsDeleted && l.Account!.Code == code)
            .ToListAsync();
        return lines.Sum(l => l.Debit - l.Credit);
    }

    public async Task<Commission> ReloadAsync() =>
        await Db.Commissions.AsNoTracking().FirstAsync(c => c.Id == Commission.Id);

    public async Task LedgerBalancesAsync()
    {
        var lines = await Db.JournalLines.Where(l => !l.IsDeleted).ToListAsync();
        lines.Sum(l => l.Debit).Should().Be(lines.Sum(l => l.Credit));
    }
}
