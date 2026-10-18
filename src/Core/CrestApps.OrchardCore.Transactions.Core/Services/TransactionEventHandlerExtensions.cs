using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Raises the <see cref="ITransactionEventHandler"/> events the same way from every place a transaction changes. A
/// handler that throws is logged and the rest still run: the change is already saved and must not be undone because a
/// consumer could not react to it.
/// </summary>
public static class TransactionEventHandlerExtensions
{
    /// <summary>
    /// Tells every handler that the transaction was created.
    /// </summary>
    /// <param name="handlers">The registered handlers.</param>
    /// <param name="transaction">The transaction, as saved.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task CreatedAsync(
        this IEnumerable<ITransactionEventHandler> handlers,
        Transaction transaction,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => InvokeAsync(handlers, transaction, (handler, ct) => handler.CreatedAsync(transaction, ct), nameof(ITransactionEventHandler.CreatedAsync), logger, cancellationToken);

    /// <summary>
    /// Tells every handler that the status of the transaction changed.
    /// </summary>
    /// <param name="handlers">The registered handlers.</param>
    /// <param name="transaction">The transaction, as saved with its new status.</param>
    /// <param name="previous">The status the transaction had before the save.</param>
    /// <param name="current">The status the transaction has now.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task StatusChangedAsync(
        this IEnumerable<ITransactionEventHandler> handlers,
        Transaction transaction,
        TransactionStatus previous,
        TransactionStatus current,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => InvokeAsync(handlers, transaction, (handler, ct) => handler.StatusChangedAsync(transaction, previous, current, ct), nameof(ITransactionEventHandler.StatusChangedAsync), logger, cancellationToken);

    /// <summary>
    /// Tells every handler that the transaction was canceled.
    /// </summary>
    /// <param name="handlers">The registered handlers.</param>
    /// <param name="transaction">The canceled transaction, as saved.</param>
    /// <param name="reason">The reason given for canceling it, when one was given.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task CanceledAsync(
        this IEnumerable<ITransactionEventHandler> handlers,
        Transaction transaction,
        string reason,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => InvokeAsync(handlers, transaction, (handler, ct) => handler.CanceledAsync(transaction, reason, ct), nameof(ITransactionEventHandler.CanceledAsync), logger, cancellationToken);

    /// <summary>
    /// Tells every handler that the transaction became overdue.
    /// </summary>
    /// <param name="handlers">The registered handlers.</param>
    /// <param name="transaction">The overdue transaction, as saved.</param>
    /// <param name="logger">The logger for handler failures.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task BecameOverdueAsync(
        this IEnumerable<ITransactionEventHandler> handlers,
        Transaction transaction,
        ILogger logger,
        CancellationToken cancellationToken = default)
        => InvokeAsync(handlers, transaction, (handler, ct) => handler.BecameOverdueAsync(transaction, ct), nameof(ITransactionEventHandler.BecameOverdueAsync), logger, cancellationToken);

    /// <summary>
    /// Returns the <see cref="TransactionEvent.Reason"/> recorded by the newest <see cref="TransactionEventType.Canceled"/>
    /// event on the timeline of the transaction, or <see langword="null"/> when no reason was given.
    /// </summary>
    /// <param name="transaction">The canceled transaction.</param>
    public static string GetCancellationReason(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var reason = transaction.Events.LastOrDefault(evt => evt.Type == TransactionEventType.Canceled)?.Reason;

        return string.IsNullOrWhiteSpace(reason)
            ? null
            : reason;
    }

    private static async Task InvokeAsync(
        IEnumerable<ITransactionEventHandler> handlers,
        Transaction transaction,
        Func<ITransactionEventHandler, CancellationToken, Task> raise,
        string eventName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await raise(handler, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "A transaction event handler '{Handler}' failed in '{Event}' for transaction '{TransactionId}'; the change itself stays saved.", handler.GetType().Name, eventName, transaction.ItemId);
            }
        }
    }
}
