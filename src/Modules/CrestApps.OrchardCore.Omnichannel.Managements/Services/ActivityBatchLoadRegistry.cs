using System.Collections.Concurrent;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Knows which activity loads this process is running right now, so the watchdog never resumes a load that is still
/// alive here, however long it has gone between committed pages.
/// </summary>
public sealed class ActivityBatchLoadRegistry
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// Records that this process has started running the load.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <returns><see langword="false"/> when this process is already running it.</returns>
    public bool TryBegin(string batchId)
        => _running.TryAdd(batchId, 0);

    /// <summary>
    /// Records that this process has stopped running the load.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    public void End(string batchId)
        => _running.TryRemove(batchId, out _);

    /// <summary>
    /// Gets whether this process is running the load.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    public bool IsRunning(string batchId)
        => _running.ContainsKey(batchId);
}
