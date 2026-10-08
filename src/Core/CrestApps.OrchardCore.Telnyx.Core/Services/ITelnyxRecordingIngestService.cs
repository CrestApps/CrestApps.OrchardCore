namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Processes durable Telnyx recording ingest jobs: it downloads each due recording from Telnyx and stores it,
/// encrypted, in the media store, retrying transient failures with back-off and dead-lettering recordings that
/// never become ingestible. This is the failed-upload recovery mechanism behind recording governance.
/// </summary>
public interface ITelnyxRecordingIngestService
{
    /// <summary>
    /// Processes the recording ingest jobs that are currently due, isolating each job so a single poison job
    /// cannot block the rest of the batch.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of recordings successfully ingested during this sweep.</returns>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}
