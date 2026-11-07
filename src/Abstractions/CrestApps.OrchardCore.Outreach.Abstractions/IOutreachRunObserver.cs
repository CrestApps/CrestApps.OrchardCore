using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Hears how an outreach run is getting on, so the record it sends for (a broadcast, a campaign) shows its progress.
/// </summary>
public interface IOutreachRunObserver
{
    /// <summary>
    /// Called after a batch's outcome was added to the run, and when the run ends, in the same unit of work as the
    /// change; the observer's own changes are saved with it.
    /// </summary>
    /// <param name="run">The run, with its counters and status.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RunUpdatedAsync(OutreachRun run, CancellationToken cancellationToken = default);
}
