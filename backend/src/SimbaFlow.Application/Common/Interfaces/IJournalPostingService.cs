using SimbaFlow.Domain.Entities.Finance;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>One side of a journal line, by account code. Exactly one of Debit/Credit is non-zero.</summary>
public record JournalLineSpec(string AccountCode, decimal Debit, decimal Credit, string? Memo = null);

public interface IJournalPostingService
{
    /// <summary>
    /// Adds a balanced entry to the context (not saved). Zero lines are dropped; throws when the
    /// lines do not balance or an account code is unknown. Returns null when nothing is left to post.
    /// </summary>
    Task<JournalEntry?> PostAsync(
        string description,
        string sourceType,
        Guid? sourceId,
        IReadOnlyList<JournalLineSpec> lines,
        Guid postedByUserId,
        DateTime? postedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings the ledger in line with the commission's current fees: posts the difference between
    /// what is charged now and what earlier fee entries already posted. Dr receivable, Cr the
    /// revenue or pass-through account for each fee type. Returns null when nothing changed.
    /// </summary>
    Task<JournalEntry?> SyncCommissionFeesAsync(
        Commission commission,
        Guid postedByUserId,
        DateTime? postedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts Cash (1100) Dr / Receivable (1200) Cr for a commission payment.
    /// Caller must run inside the same DB transaction as the payment insert.
    /// </summary>
    Task<JournalEntry> PostCommissionPaymentAsync(
        Payment payment,
        Commission commission,
        Guid postedByUserId,
        CancellationToken cancellationToken = default);
}
