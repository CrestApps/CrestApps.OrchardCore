using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// A base for an <see cref="ITransactionEventHandler"/> that implements every event as doing nothing, so a handler
/// overrides only the events it reacts to.
/// </summary>
public abstract class TransactionEventHandlerBase : ITransactionEventHandler
{
    /// <inheritdoc/>
    public virtual Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
