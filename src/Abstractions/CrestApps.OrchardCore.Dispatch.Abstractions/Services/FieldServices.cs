using CrestApps.OrchardCore.Dispatch.Models;

namespace CrestApps.OrchardCore.Dispatch.Services;

/// <summary>
/// Serves the technician app: the visits it keeps offline, and the changes it sends back once it can.
/// </summary>
public interface IFieldSyncService
{
    /// <summary>
    /// Gets a worker's visits from today, with their work orders, checklists and parts, to keep on the device.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The pack, or <see langword="null"/> when the worker doesn't exist.</returns>
    Task<FieldPack> GetPackAsync(string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a worker's commands in their order. A command sent before is not applied again, and commands for
    /// a visit the worker is no longer on, or that was canceled, are refused with the reason; the schedule on the
    /// server wins.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="commands">The commands, oldest first.</param>
    /// <param name="actor">Who sent them, for the history.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A result for each command, in the same order.</returns>
    Task<IReadOnlyList<FieldCommandResult>> ApplyAsync(string workerId, IReadOnlyList<FieldCommand> commands, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps technicians' positions, only with their consent and only while they work.
/// </summary>
public interface IWorkerLocationService
{
    /// <summary>
    /// Gets whether a worker agreed to the current notice.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Whether they agreed.</returns>
    Task<bool> HasConsentAsync(string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a worker agreed to the current notice, or withdrew; withdrawing removes their positions.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="given">Whether they agreed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The consent record.</returns>
    Task<WorkerLocationConsent> SetConsentAsync(string workerId, bool given, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps a worker's positions when they agreed and are working: on the way to, at or between visits of the day.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="pings">The positions.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were kept, and why the rest weren't.</returns>
    Task<LocationPingResult> RecordAsync(string workerId, IReadOnlyList<LocationPingInput> pings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets each worker's latest position since a time.
    /// </summary>
    /// <param name="workerIds">The workers.</param>
    /// <param name="sinceUtc">The earliest position to give.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The latest position of each worker that has one.</returns>
    Task<IReadOnlyList<WorkerLocationPing>> LatestAsync(IReadOnlyCollection<string> workerIds, DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes positions and command records older than they are kept for.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were removed.</returns>
    Task<int> PruneAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Keeps the photos and signatures the technician app sends.
/// </summary>
public interface IFieldFileStore
{
    /// <summary>
    /// Saves a file.
    /// </summary>
    /// <param name="path">Where, such as <c>work-orders/WO-000001/photo.jpg</c>.</param>
    /// <param name="content">The content.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The saved file's path, to keep on the work order.</returns>
    Task<string> SaveAsync(string path, Stream content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lists the stock locations a technician can use parts from.
/// </summary>
public interface IFieldStockSource
{
    /// <summary>
    /// Lists the locations, the technician's own first.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The locations.</returns>
    Task<IReadOnlyList<FieldStockLocation>> ListAsync(string workerId, CancellationToken cancellationToken = default);
}
