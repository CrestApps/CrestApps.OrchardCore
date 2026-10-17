using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// An <see cref="ITransactionEventHandler"/> that records every event it is told about, so a test can assert exactly
/// which lifecycle events a change raised.
/// </summary>
internal sealed class RecordingTransactionEventHandler : ITransactionEventHandler
{
    public List<string> Created { get; } = [];

    public List<(string TransactionId, TransactionStatus Previous, TransactionStatus Current)> StatusChanges { get; } = [];

    public List<(string TransactionId, string Reason)> Canceled { get; } = [];

    public List<string> Overdue { get; } = [];

    public int Count
        => Created.Count + StatusChanges.Count + Canceled.Count + Overdue.Count;

    public Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        Created.Add(transaction.ItemId);

        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        StatusChanges.Add((transaction.ItemId, previous, current));

        return Task.CompletedTask;
    }

    public Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        Canceled.Add((transaction.ItemId, reason));

        return Task.CompletedTask;
    }

    public Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        Overdue.Add(transaction.ItemId);

        return Task.CompletedTask;
    }
}
