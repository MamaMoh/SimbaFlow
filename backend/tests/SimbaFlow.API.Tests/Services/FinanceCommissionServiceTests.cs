using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.API.Features.Finance.Validators;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Example-based tests for Unit 5 Finance &amp; Commission (TEST-50–58).
/// </summary>
public class FinanceCommissionServiceTests
{
    private static async Task<(
        TenantDbContext Db,
        ICurrentUserService User,
        Guid UserId,
        Commission Commission,
        IExchangeRateService Fx,
        IJournalPostingService Journal)> CreateSutAsync()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"finance_{Guid.NewGuid()}")
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var userId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.UserName.Returns("finance.tester");
        currentUser.TenantId.Returns(Guid.NewGuid());

        var db = new TenantDbContext(options, currentUser);

        // InMemory does not emulate PostgreSQL xmin; concurrency tokens aren't required for these unit tests.

        var candidate = new Candidate
        {
            FirstName = "Abebe",
            LastName = "Kebede",
            PassportNumber = "EP1234567",
            DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = Gender.Male,
            OfficeId = Guid.NewGuid(),
            Status = CandidateStatus.Active,
            RegisteredAt = DateTime.UtcNow
        };
        db.Candidates.Add(candidate);

        var commission = new Commission
        {
            CandidateId = candidate.Id,
            Status = CommissionStatus.Open,
            OfficeName = "Riyadh Partner",
            CountryOfTravel = "Saudi Arabia",
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId
        };
        db.Commissions.Add(commission);

        db.Accounts.AddRange(
            new Account
            {
                Code = FinanceSeedService.CashBankCode,
                Name = "Cash / Bank",
                Type = AccountType.Asset,
                IsSystem = true,
                IsActive = true
            },
            new Account
            {
                Code = FinanceSeedService.CommissionRevenueCode,
                Name = "Commission Revenue",
                Type = AccountType.Revenue,
                IsSystem = true,
                IsActive = true
            });

        await db.SaveChangesAsync();

        var fx = Substitute.For<IExchangeRateService>();
        fx.ResolveRateToEtbAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ccy = call.ArgAt<string>(0);
                return string.Equals(ccy, "ETB", StringComparison.OrdinalIgnoreCase) ? 1m : 55m;
            });
        fx.ConvertToEtbAsync(Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var amount = call.ArgAt<decimal>(0);
                var ccy = call.ArgAt<string>(1);
                var rate = string.Equals(ccy, "ETB", StringComparison.OrdinalIgnoreCase) ? 1m : 55m;
                return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
            });

        var journal = new JournalPostingService(db, NullLogger<JournalPostingService>.Instance);
        return (db, currentUser, userId, commission, fx, journal);
    }

    [Fact]
    public async Task UpsertFees_SetsTotalsAndOpenStatus_TEST50()
    {
        var (db, _, _, commission, fx, _) = await CreateSutAsync();
        var handler = new UpsertCommissionFeesHandler(db, fx);

        var result = await handler.Handle(new UpsertCommissionFeesCommand(commission.Id,
        [
            new FeeLineInput("AgencyFee", "Placement", 1000m, "ETB", 0),
            new FeeLineInput("Medical", null, 200m, "ETB", 1)
        ]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await db.Commissions.Include(c => c.Fees).FirstAsync(c => c.Id == commission.Id);
        reloaded.TotalFeesAmount.Should().Be(1200m);
        reloaded.TotalPaidAmount.Should().Be(0);
        reloaded.BalanceAmount.Should().Be(1200m);
        reloaded.Status.Should().Be(CommissionStatus.Open);
        reloaded.Fees.Count(f => !f.IsDeleted).Should().Be(2);
    }

    [Fact]
    public async Task RecordPayment_RequiresFees_TEST51()
    {
        var (db, user, _, commission, fx, journal) = await CreateSutAsync();
        var handler = new RecordPaymentHandler(db, fx, journal, user);

        var result = await handler.Handle(new RecordPaymentCommand(
            commission.Id, 100m, "ETB", "Cash", null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("fees");
    }

    [Fact]
    public async Task RecordPayment_PostsBalancedJournalAndPartialStatus_TEST52()
    {
        var (db, user, _, commission, fx, journal) = await CreateSutAsync();
        await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 1000m, "ETB", 0)]),
            CancellationToken.None);

        var pay = await new RecordPaymentHandler(db, fx, journal, user).Handle(
            new RecordPaymentCommand(commission.Id, 400m, "ETB", "Cash", null, "REF-1", null),
            CancellationToken.None);

        pay.IsSuccess.Should().BeTrue();

        var reloaded = await db.Commissions
            .Include(c => c.Payments)
            .FirstAsync(c => c.Id == commission.Id);
        reloaded.TotalPaidAmount.Should().Be(400m);
        reloaded.BalanceAmount.Should().Be(600m);
        reloaded.Status.Should().Be(CommissionStatus.Partial);

        var payment = reloaded.Payments.Single(p => !p.IsDeleted);
        payment.JournalEntryId.Should().NotBeNull();

        var entry = await db.JournalEntries
            .Include(j => j.Lines)
            .FirstAsync(j => j.Id == payment.JournalEntryId);
        entry.Lines.Where(l => !l.IsDeleted).Sum(l => l.Debit)
            .Should().Be(entry.Lines.Where(l => !l.IsDeleted).Sum(l => l.Credit));
        entry.Lines.Where(l => !l.IsDeleted).Sum(l => l.Debit).Should().Be(400m);
    }

    [Fact]
    public async Task RecordPayment_SettlesWhenPaidInFull_TEST53()
    {
        var (db, user, _, commission, fx, journal) = await CreateSutAsync();
        await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 500m, "ETB", 0)]),
            CancellationToken.None);

        var pay = await new RecordPaymentHandler(db, fx, journal, user).Handle(
            new RecordPaymentCommand(commission.Id, 500m, "ETB", "BankTransfer", null, null, null),
            CancellationToken.None);

        pay.IsSuccess.Should().BeTrue();
        var reloaded = await db.Commissions.FirstAsync(c => c.Id == commission.Id);
        reloaded.Status.Should().Be(CommissionStatus.Settled);
        reloaded.BalanceAmount.Should().Be(0);
    }

    [Fact]
    public async Task UpsertFees_BlockedWhenSettled_TEST54()
    {
        var (db, user, _, commission, fx, journal) = await CreateSutAsync();
        await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 100m, "ETB", 0)]),
            CancellationToken.None);
        await new RecordPaymentHandler(db, fx, journal, user).Handle(
            new RecordPaymentCommand(commission.Id, 100m, "ETB", "Cash", null, null, null),
            CancellationToken.None);

        var result = await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 200m, "ETB", 0)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Settled");
    }

    [Fact]
    public async Task OpenDispute_SetsDisputed_AndResolveRecalcs_TEST55()
    {
        var (db, user, _, commission, fx, _) = await CreateSutAsync();
        await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 300m, "ETB", 0)]),
            CancellationToken.None);

        var open = await new OpenDisputeHandler(db, user).Handle(
            new OpenDisputeCommand(commission.Id, "Sponsor dispute"), CancellationToken.None);
        open.IsSuccess.Should().BeTrue();

        var disputed = await db.Commissions.FirstAsync(c => c.Id == commission.Id);
        disputed.Status.Should().Be(CommissionStatus.Disputed);

        var second = await new OpenDisputeHandler(db, user).Handle(
            new OpenDisputeCommand(commission.Id, "Another"), CancellationToken.None);
        second.IsSuccess.Should().BeFalse();

        var resolve = await new ResolveDisputeHandler(db, user).Handle(
            new ResolveDisputeCommand(open.Data!, "Agreed settlement"), CancellationToken.None);
        resolve.IsSuccess.Should().BeTrue();

        var after = await db.Commissions.FirstAsync(c => c.Id == commission.Id);
        after.Status.Should().Be(CommissionStatus.Open);
    }

    [Fact]
    public async Task NonEtbPayment_UsesFxRate_TEST56()
    {
        var (db, user, _, commission, fx, journal) = await CreateSutAsync();
        await new UpsertCommissionFeesHandler(db, fx).Handle(
            new UpsertCommissionFeesCommand(commission.Id,
            [new FeeLineInput("AgencyFee", null, 5500m, "ETB", 0)]),
            CancellationToken.None);

        var pay = await new RecordPaymentHandler(db, fx, journal, user).Handle(
            new RecordPaymentCommand(commission.Id, 100m, "USD", "Cash", null, null, null),
            CancellationToken.None);

        pay.IsSuccess.Should().BeTrue();
        var payment = await db.Payments.FirstAsync(p => p.Id == pay.Data);
        payment.ExchangeRateToEtb.Should().Be(55m);
        payment.AmountEtb.Should().Be(5500m);
    }

    [Fact]
    public void Validators_EnforceAmountAndReason_TEST57()
    {
        new RecordPaymentValidator()
            .Validate(new RecordPaymentCommand(Guid.NewGuid(), 0, "ETB", "Cash", null, null, null))
            .IsValid.Should().BeFalse();

        new OpenDisputeValidator()
            .Validate(new OpenDisputeCommand(Guid.NewGuid(), ""))
            .IsValid.Should().BeFalse();

        new ResolveDisputeValidator()
            .Validate(new ResolveDisputeCommand(Guid.NewGuid(), ""))
            .IsValid.Should().BeFalse();
    }
}
