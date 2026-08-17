using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Sends outstanding-payment reminders to the owner of a <see cref="Transaction"/>. The default
/// implementation delivers the reminder through the notification system so it honors each user's channel
/// preference (email, and any other configured channel) rather than assuming email only.
/// </summary>
public interface ITransactionReminderService
{
    /// <summary>
    /// Sends a payment reminder for the supplied transaction to its owner, records a
    /// <see cref="TransactionEventType.ReminderSent"/> event, and updates the reminder counters. The
    /// transaction is persisted by the caller after a successful send is reflected on it.
    /// </summary>
    /// <param name="transaction">The outstanding transaction to remind about.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a reminder was sent; otherwise <see langword="false"/>.</returns>
    Task<bool> SendReminderAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
