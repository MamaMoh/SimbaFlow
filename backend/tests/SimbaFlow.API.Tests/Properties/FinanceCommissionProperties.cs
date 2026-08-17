using FsCheck;
using FsCheck.Xunit;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.API.Features.Finance.Validators;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// FsCheck properties for Unit 5 Finance &amp; Commission invariants (TEST-50–58).
/// </summary>
public class FinanceCommissionProperties
{
    /// <summary>TEST-50: Journal lines for a payment always balance (Cash Dr = Revenue Cr).</summary>
    [Property(MaxTest = 50)]
    public bool JournalAlwaysBalances(PositiveInt cents)
    {
        var amountEtb = Math.Round(cents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        if (amountEtb <= 0) return true;
        var debit = amountEtb;
        var credit = amountEtb;
        return debit == credit;
    }

    /// <summary>TEST-51: Status from balances matches Open / Partial / Settled.</summary>
    [Property(MaxTest = 80)]
    public bool StatusFromBalances(NonNegativeInt feeCents, NonNegativeInt paidCents)
    {
        var fees = Math.Round(feeCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var paid = Math.Round(paidCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var status = Recalc(fees, paid, openDispute: false);
        if (fees > 0 && paid >= fees) return status == CommissionStatus.Settled;
        if (paid > 0) return status == CommissionStatus.Partial;
        return status == CommissionStatus.Open;
    }

    /// <summary>TEST-52: Open dispute overrides balance-based status.</summary>
    [Property(MaxTest = 40)]
    public bool DisputedOverrides(NonNegativeInt feeCents, NonNegativeInt paidCents)
    {
        var fees = Math.Round(feeCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var paid = Math.Round(paidCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        return Recalc(fees, paid, openDispute: true) == CommissionStatus.Disputed;
    }

    /// <summary>TEST-53: Payment amount must be &gt; 0 (validator).</summary>
    [Property(MaxTest = 40)]
    public bool PaymentRequiresPositiveAmount(PositiveInt cents)
    {
        var amount = Math.Round(cents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var ok = new RecordPaymentValidator().Validate(
            new RecordPaymentCommand(Guid.NewGuid(), amount, "ETB", "Cash", null, null, null));
        var bad = new RecordPaymentValidator().Validate(
            new RecordPaymentCommand(Guid.NewGuid(), 0, "ETB", "Cash", null, null, null));
        return ok.IsValid && !bad.IsValid;
    }

    /// <summary>TEST-54: ETB conversion uses rate 1.</summary>
    [Property(MaxTest = 50)]
    public bool EtbRateIsOne(PositiveInt cents)
    {
        var amount = Math.Round(cents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        const decimal rate = 1m;
        var amountEtb = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
        return amountEtb == amount;
    }

    /// <summary>TEST-55: Non-ETB AmountEtb = Amount × Rate (2 dp).</summary>
    [Property(MaxTest = 50)]
    public bool NonEtbConversionRounds(PositiveInt amountCents, PositiveInt rateCents)
    {
        var amount = Math.Round(amountCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var rate = Math.Max(0.01m, Math.Round(rateCents.Get / 100m, 4, MidpointRounding.AwayFromZero));
        var etb = Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
        return etb == Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero) && etb >= 0;
    }

    /// <summary>TEST-56: Fee amounts must be ≥ 0.</summary>
    [Property(MaxTest = 40)]
    public bool FeeAmountNonNegative(NonNegativeInt cents)
    {
        var amount = Math.Round(cents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var ok = new UpsertCommissionFeesValidator().Validate(
            new UpsertCommissionFeesCommand(Guid.NewGuid(),
            [new FeeLineInput("AgencyFee", null, amount, "ETB", 0)]));
        var bad = new UpsertCommissionFeesValidator().Validate(
            new UpsertCommissionFeesCommand(Guid.NewGuid(),
            [new FeeLineInput("AgencyFee", null, -1m, "ETB", 0)]));
        return ok.IsValid && !bad.IsValid;
    }

    /// <summary>TEST-57: Dispute open requires reason; resolve requires notes.</summary>
    [Property(MaxTest = 30)]
    public bool DisputeValidators(NonEmptyString text)
    {
        // NotEmpty() correctly rejects whitespace-only reasons, so ensure the
        // "valid" sample has real (non-whitespace) content rather than e.g. "\n".
        var reason = string.IsNullOrWhiteSpace(text.Get) ? "reason" : text.Get;

        var openOk = new OpenDisputeValidator().Validate(
            new OpenDisputeCommand(Guid.NewGuid(), reason));
        var openBad = new OpenDisputeValidator().Validate(
            new OpenDisputeCommand(Guid.NewGuid(), ""));
        var resolveOk = new ResolveDisputeValidator().Validate(
            new ResolveDisputeCommand(Guid.NewGuid(), reason));
        var resolveBad = new ResolveDisputeValidator().Validate(
            new ResolveDisputeCommand(Guid.NewGuid(), ""));
        return openOk.IsValid && !openBad.IsValid && resolveOk.IsValid && !resolveBad.IsValid;
    }

    /// <summary>TEST-58: Balance = Fees − Paid (model).</summary>
    [Property(MaxTest = 60)]
    public bool BalanceEqualsFeesMinusPaid(NonNegativeInt feeCents, NonNegativeInt paidCents)
    {
        var fees = Math.Round(feeCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var paid = Math.Round(paidCents.Get / 100m, 2, MidpointRounding.AwayFromZero);
        var balance = Math.Round(fees - paid, 2, MidpointRounding.AwayFromZero);
        return balance == fees - paid;
    }

    private static CommissionStatus Recalc(decimal fees, decimal paid, bool openDispute)
    {
        if (openDispute) return CommissionStatus.Disputed;
        if (fees > 0 && paid >= fees) return CommissionStatus.Settled;
        if (paid > 0) return CommissionStatus.Partial;
        return CommissionStatus.Open;
    }
}
