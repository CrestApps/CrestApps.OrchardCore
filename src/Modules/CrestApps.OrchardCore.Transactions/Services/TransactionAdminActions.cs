using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The admin actions of the Transactions pages that are not commands of <see cref="ITransactionCommandService"/>,
/// shared by the pages and the Transactions AI tools: adding a note to a transaction, settling by hand a refund that waits
/// for manual review, and resolving a payment issue.
/// </summary>
internal static class TransactionAdminActions
{
    /// <summary>
    /// Adds an internal note to a transaction's timeline, as the transaction's page does. The caller saves it.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="note">The note.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="actorId">The id of the user who writes it.</param>
    /// <param name="actorName">The name of the user who writes it.</param>
    public static void AddNote(Transaction transaction, string note, DateTime utcNow, string actorId, string actorName)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        transaction.UpdatedUtc = utcNow;
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = utcNow,
            Type = TransactionEventType.Note,
            Message = note,
            ActorId = actorId,
            ActorName = actorName,
        });
    }

    /// <summary>
    /// Marks a refund that waits for manual review as settled, with the reference of the money returned by hand, and runs
    /// the refund handlers, as the Refunds page does. Only a refund waiting for manual review can be settled this way:
    /// allowing it on any refund would let a failed gateway refund be marked done when the customer got nothing.
    /// </summary>
    /// <param name="services">The services the refund handlers are resolved from.</param>
    /// <param name="refundStore">The refund store.</param>
    /// <param name="refund">The refund.</param>
    /// <param name="reference">The reference of the money returned by hand, or <see langword="null"/> to keep the stored one.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="false"/> when the refund does not wait for manual review.</returns>
    public static async Task<bool> SettleRefundByHandAsync(
        IServiceProvider services,
        IPaymentRefundStore refundStore,
        PaymentRefund refund,
        string reference,
        DateTime utcNow,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refund);

        if (refund.Status != RefundStatus.PendingManualReview)
        {
            return false;
        }

        var previousStatus = refund.Status;

        refund.Status = RefundStatus.Succeeded;
        refund.CompletedUtc = utcNow;
        refund.ProviderRefundReference = string.IsNullOrWhiteSpace(reference) ? refund.ProviderRefundReference : reference.Trim();

        await refundStore.UpdateAsync(refund, cancellationToken);
        await services.RefundSucceededAsync(refund, previousStatus, logger, cancellationToken);

        return true;
    }

    /// <summary>
    /// Marks an open payment issue as resolved with a note, as the Payment issues page does. It performs no payment
    /// operation.
    /// </summary>
    /// <param name="issueStore">The payment issue store.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="note">What was done, or <see langword="null"/>.</param>
    /// <param name="resolvedBy">The name of the user who resolves it.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="false"/> when the issue was already resolved.</returns>
    public static async Task<bool> ResolveIssueAsync(IPaymentIssueStore issueStore, PaymentIssue issue, string note, string resolvedBy, DateTime utcNow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issue);

        if (issue.Status != PaymentIssueStatus.Open)
        {
            return false;
        }

        issue.Status = PaymentIssueStatus.Resolved;
        issue.ResolvedUtc = utcNow;
        issue.ResolvedBy = resolvedBy;
        issue.ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await issueStore.UpdateAsync(issue, cancellationToken);

        return true;
    }
}
