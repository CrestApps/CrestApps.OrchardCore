using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Keeps the records of commands from the technician app in the Dispatch collection.
/// </summary>
public sealed class FieldCommandReceiptStore : DocumentCatalog<FieldCommandReceipt, FieldCommandReceiptIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldCommandReceiptStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public FieldCommandReceiptStore(ISession session)
        : base(session)
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <summary>
    /// Finds the records of a worker's commands by their keys.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records found.</returns>
    public async Task<IReadOnlyList<FieldCommandReceipt>> FindAsync(string workerId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        var receipts = await Session.Query<FieldCommandReceipt, FieldCommandReceiptIndex>(index => index.WorkerId == workerId && index.Key.IsIn(keys), collection: CollectionName)
            .ListAsync(cancellationToken);

        return [.. receipts];
    }

    /// <summary>
    /// Removes records that came in before a time, a batch at a time.
    /// </summary>
    /// <param name="beforeUtc">The time.</param>
    /// <param name="max">The most to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were removed.</returns>
    public async Task<int> PruneAsync(DateTime beforeUtc, int max, CancellationToken cancellationToken = default)
    {
        var old = await Session.Query<FieldCommandReceipt, FieldCommandReceiptIndex>(index => index.ReceivedUtc < beforeUtc, collection: CollectionName)
            .Take(max)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var receipt in old)
        {
            Session.Delete(receipt, CollectionName);
            count++;
        }

        return count;
    }
}

/// <summary>
/// Keeps workers' location consent in the Dispatch collection.
/// </summary>
public sealed class WorkerLocationConsentStore : DocumentCatalog<WorkerLocationConsent, WorkerLocationConsentIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerLocationConsentStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public WorkerLocationConsentStore(ISession session)
        : base(session)
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <summary>
    /// Finds a worker's consent.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The consent, or <see langword="null"/> when they were never asked.</returns>
    public Task<WorkerLocationConsent> FindByWorkerAsync(string workerId, CancellationToken cancellationToken = default)
        => Session.Query<WorkerLocationConsent, WorkerLocationConsentIndex>(index => index.WorkerId == workerId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Keeps technicians' positions in the Dispatch collection.
/// </summary>
public sealed class WorkerLocationPingStore : DocumentCatalog<WorkerLocationPing, WorkerLocationPingIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerLocationPingStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public WorkerLocationPingStore(ISession session)
        : base(session)
    {
        CollectionName = DispatchConstants.CollectionName;
    }

    /// <summary>
    /// Gets the positions of workers since a time, newest first, from the index.
    /// </summary>
    /// <param name="workerIds">The workers.</param>
    /// <param name="sinceUtc">The earliest.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The positions.</returns>
    public async Task<IReadOnlyList<WorkerLocationPingIndex>> ListSinceAsync(IReadOnlyCollection<string> workerIds, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        if (workerIds.Count == 0)
        {
            return [];
        }

        var rows = await Session.QueryIndex<WorkerLocationPingIndex>(index => index.WorkerId.IsIn(workerIds) && index.Utc >= sinceUtc, collection: CollectionName)
            .OrderByDescending(index => index.Utc)
            .ListAsync(cancellationToken);

        return [.. rows];
    }

    /// <summary>
    /// Removes positions taken before a time, or all of one worker's, a batch at a time.
    /// </summary>
    /// <param name="beforeUtc">The time.</param>
    /// <param name="workerId">Only this worker's, at any time, or <see langword="null"/>.</param>
    /// <param name="max">The most to remove.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were removed.</returns>
    public async Task<int> PruneAsync(DateTime beforeUtc, string workerId, int max, CancellationToken cancellationToken = default)
    {
        var query = workerId is null
            ? Session.Query<WorkerLocationPing, WorkerLocationPingIndex>(index => index.Utc < beforeUtc, collection: CollectionName)
            : Session.Query<WorkerLocationPing, WorkerLocationPingIndex>(index => index.WorkerId == workerId, collection: CollectionName);
        var old = await query.Take(max).ListAsync(cancellationToken);
        var count = 0;

        foreach (var ping in old)
        {
            Session.Delete(ping, CollectionName);
            count++;
        }

        return count;
    }
}
