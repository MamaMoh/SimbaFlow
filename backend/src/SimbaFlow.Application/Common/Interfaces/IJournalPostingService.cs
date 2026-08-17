using SimbaFlow.Domain.Entities.Finance;

namespace SimbaFlow.Application.Common.Interfaces;

public interface IJournalPostingService
{
    /// <summary>
    /// Posts Cash (1100) Dr / Revenue (4100) Cr for a commission payment.
    /// Caller must run inside the same DB transaction as the payment insert.
    /// </summary>
    Task<JournalEntry> PostCommissionPaymentAsync(
        Payment payment,
        Commission commission,
        Guid postedByUserId,
        CancellationToken cancellationToken = default);
}
