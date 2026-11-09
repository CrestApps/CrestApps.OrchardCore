using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// Starts and stops outreach runs. The engine sends a run in batches on the leased work queue: each batch reads one page
/// of the audience, skips recipients a policy rules out, renders the template and sends, and holds the batch while the
/// run's channel is in quiet hours.
/// </summary>
public interface IOutreachEngine
{
    /// <summary>
    /// Stores a new run and queues its first batch, in the caller's unit of work.
    /// </summary>
    /// <param name="run">The run: its owner, audience source, sender and template. Its counters and
    /// <see cref="OutreachRun.Cursor"/> may carry progress made before the run existed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stored run.</returns>
    Task<OutreachRun> StartAsync(OutreachRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a run.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The run, or <see langword="null"/>.</returns>
    Task<OutreachRun> FindRunAsync(string runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a run: no further batch is sent.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the run was stopped; <see langword="false"/> when it had already ended or does not exist.</returns>
    Task<bool> CancelAsync(string runId, CancellationToken cancellationToken = default);
}
