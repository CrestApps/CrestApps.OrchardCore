using CrestApps.OrchardCore.Telnyx.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Durable, per-tenant store for Telnyx recording ingest jobs. Each mutating operation commits in its own
/// isolated session so a job becomes durable immediately, independent of the ambient request scope,
/// guaranteeing that a recording queued for ingestion survives an application restart.
/// </summary>
public interface ITelnyxRecordingIngestJobStore
{
    /// <summary>
    /// Enqueues a recording for durable ingestion. The operation is idempotent per recording id: if a job for
    /// the recording already exists (pending, completed, or dead-lettered) no duplicate is created.
    /// </summary>
    /// <param name="interactionId">The identifier of the interaction the recording belongs to.</param>
    /// <param name="recordingId">The Telnyx recording id that addresses the stored recording.</param>
    /// <param name="format">The media format the recording is stored in.</param>
    /// <param name="nowUtc">The current UTC time used to stamp the job's creation and first-due time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task EnqueueAsync(
        string interactionId,
        string recordingId,
        string format,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the pending jobs whose next attempt is due at or before the supplied time.
    /// </summary>
    /// <param name="nowUtc">The current UTC time used to select due jobs.</param>
    /// <param name="maxCount">The maximum number of jobs to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due pending jobs, ordered by their next attempt time.</returns>
    Task<IReadOnlyList<TelnyxRecordingIngestJob>> GetDueAsync(
        DateTime nowUtc,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the mutable state of a job (status, attempt count, next attempt time, media reference, and last
    /// error) durably in its own isolated session.
    /// </summary>
    /// <param name="job">The job whose state should be persisted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task UpdateAsync(TelnyxRecordingIngestJob job, CancellationToken cancellationToken = default);
}
