using CrestApps.Core.Support;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Default <see cref="IAsteriskRecordingIngestService"/> implementation. It downloads each due recording from
/// Asterisk through the ARI stored-file endpoint and persists it, encrypted at rest, in the pluggable media
/// store. A recording that is not yet readable (or a transient store failure) is retried with exponential
/// back-off; a recording that never becomes ingestible is dead-lettered after the attempt budget is exhausted
/// so it is never retried indefinitely and is never silently lost. A recording whose interaction has already
/// had its recording erased is never (re-)ingested; any media written for it is cleaned up and the job is
/// cancelled so a late ingest cannot resurrect deleted media.
/// </summary>
internal sealed class AsteriskRecordingIngestService : IAsteriskRecordingIngestService
{
    private readonly IAsteriskRecordingIngestJobStore _jobStore;
    private readonly IAsteriskAriClient _ariClient;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IRecordingErasureGuard _erasureGuard;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskRecordingIngestService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskRecordingIngestService"/> class.
    /// </summary>
    /// <param name="jobStore">The durable recording ingest job store.</param>
    /// <param name="ariClient">The tenant-scoped Asterisk ARI client used to download the stored recording.</param>
    /// <param name="mediaStore">The media store that persists recordings encrypted at rest.</param>
    /// <param name="erasureGuards">
    /// The optional recording erasure guards. When the Contact Center recording governance feature is enabled the
    /// first guard is consulted to refuse ingesting a recording that has already been erased; when absent, ingest
    /// proceeds unchanged.
    /// </param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger instance.</param>
    public AsteriskRecordingIngestService(
        IAsteriskRecordingIngestJobStore jobStore,
        IAsteriskAriClient ariClient,
        IRecordingMediaStore mediaStore,
        IEnumerable<IRecordingErasureGuard> erasureGuards,
        IClock clock,
        ILogger<AsteriskRecordingIngestService> logger)
    {
        _jobStore = jobStore;
        _ariClient = ariClient;
        _mediaStore = mediaStore;
        _erasureGuard = erasureGuards.FirstOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var dueJobs = await _jobStore.ListDueAsync(nowUtc, AsteriskAriConstants.RecordingIngestBatchSize, cancellationToken);
        var ingested = 0;

        foreach (var job in dueJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await TryIngestAsync(job, nowUtc, cancellationToken))
                {
                    ingested++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unexpected error occurred while ingesting recording {RecordingName}.",
                    job.RecordingName.SanitizeLogValue());

                await RecordFailureAsync(job, nowUtc, "An unexpected error occurred during ingestion.", cancellationToken);
            }
        }

        return ingested;
    }

    private async Task<bool> TryIngestAsync(AsteriskRecordingIngestJob job, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Refuse to ingest a recording whose interaction has already been erased (or no longer exists) so a late
        // job can never resurrect deleted media. This is checked before any download or store work is done.
        if (await IsRecordingErasedAsync(job.InteractionId, cancellationToken))
        {
            return await CancelErasedIngestAsync(job, nowUtc, cancellationToken);
        }

        // The encrypted store happens at most once per job. If a prior attempt already stored the recording but
        // failed to clean up the plaintext source, the retry skips straight to source cleanup instead of
        // re-downloading and re-storing the same recording.
        if (!job.MediaStored)
        {
            AsteriskAriStoredRecordingContent content;

            try
            {
                content = await _ariClient.DownloadStoredRecordingAsync(job.RecordingName, cancellationToken);
            }
            catch (AsteriskAriException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Downloading recording {RecordingName} from Asterisk failed; it will be retried.",
                    job.RecordingName.SanitizeLogValue());

                await RecordFailureAsync(job, nowUtc, "The stored recording could not be downloaded from Asterisk.", cancellationToken);

                return false;
            }

            // A null download means the stored file is not readable yet (still flushing) or was already removed by
            // retention. Either way the job is retried with back-off; a file that never appears is eventually
            // dead-lettered rather than retried forever.
            if (content is null)
            {
                await RecordFailureAsync(job, nowUtc, "The stored recording was not yet available to download.", cancellationToken);

                return false;
            }

            // The download response is held open only for the duration of the store so the recording streams
            // straight from Asterisk into the encrypting media store without being buffered whole in memory.
            await using (content)
            {
                job.MediaReference = await _mediaStore.StoreAsync(new RecordingMediaWriteRequest
                {
                    StorageKey = job.RecordingName,
                    InteractionId = job.InteractionId,
                    Format = job.Format,
                    Content = content.Content,
                }, cancellationToken);
            }

            job.MediaStored = true;

            // Durably record that the encrypted copy exists before attempting the plaintext source cleanup. If the
            // process crashes after a successful delete but before the job is marked Completed, the retry reloads a
            // job with MediaStored == true and skips the download/store, treating an ARI 404 on delete as success.
            job.ModifiedUtc = nowUtc;

            await _jobStore.UpdateAsync(job, cancellationToken);
        }

        // Re-check erasure after the media is stored: an erasure request can land during the download/store window,
        // in which case the media just written must be deleted rather than left orphaned in the store.
        if (await IsRecordingErasedAsync(job.InteractionId, cancellationToken))
        {
            return await CancelErasedIngestAsync(job, nowUtc, cancellationToken);
        }

        // The recording now lives encrypted at rest in the media store, so the unencrypted ARI source file is
        // removed to avoid leaving plaintext media on the Asterisk host. Cleanup is part of the job lifecycle: a
        // transient delete failure is retried with back-off (without re-storing the already-durable encrypted
        // copy) rather than silently leaving plaintext behind.
        try
        {
            await _ariClient.DeleteStoredRecordingAsync(job.RecordingName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The Asterisk source file for recording {RecordingName} could not be deleted after ingestion; cleanup will be retried.",
                job.RecordingName.SanitizeLogValue());

            await RecordFailureAsync(job, nowUtc, "The Asterisk source file could not be deleted after ingestion.", cancellationToken);

            return false;
        }

        job.Status = RecordingIngestJobStatus.Completed;
        job.AttemptCount++;
        job.LastError = null;
        job.ModifiedUtc = nowUtc;

        await _jobStore.UpdateAsync(job, cancellationToken);

        return true;
    }

    private async Task<bool> IsRecordingErasedAsync(string interactionId, CancellationToken cancellationToken)
        => _erasureGuard is not null && await _erasureGuard.IsRecordingErasedAsync(interactionId, cancellationToken);

    private async Task<bool> CancelErasedIngestAsync(AsteriskRecordingIngestJob job, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Deleting the media-store copy is the primary risk to close, so it is retriable: a transient failure
        // records a failure and lets the next due pass re-check erasure and retry the cancellation rather than
        // leaving erased media in the store.
        // The media store addresses recordings by their deterministic storage key (the recording name), so a crash
        // between storing the encrypted copy and persisting MediaReference/MediaStored can still be cleaned up:
        // fall back to the recording name when the reference was never durably recorded. Deletion is idempotent, so
        // an object that is already absent is treated as a confirmed delete.
        var mediaReference = string.IsNullOrEmpty(job.MediaReference) ? job.RecordingName : job.MediaReference;

        if (!string.IsNullOrEmpty(mediaReference))
        {
            try
            {
                if (!await _mediaStore.DeleteAsync(mediaReference, cancellationToken))
                {
                    await RecordFailureAsync(
                        job,
                        nowUtc,
                        "Erased recording media deletion could not be confirmed during ingest cancellation.",
                        cancellationToken);

                    return false;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Deleting erased recording media for recording {RecordingName} failed; cancellation will be retried.",
                    job.RecordingName.SanitizeLogValue());

                await RecordFailureAsync(job, nowUtc, "Erased recording media could not be deleted during ingest cancellation.", cancellationToken);

                return false;
            }
        }

        // The plaintext ARI source is transient and Asterisk ages it out independently, so its removal is
        // best-effort and never blocks cancelling the job.
        try
        {
            await _ariClient.DeleteStoredRecordingAsync(job.RecordingName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The Asterisk source file for erased recording {RecordingName} could not be deleted during ingest cancellation.",
                job.RecordingName.SanitizeLogValue());
        }

        job.Status = RecordingIngestJobStatus.Cancelled;
        job.AttemptCount++;
        job.LastError = "Ingest was cancelled because the recording was erased.";
        job.ModifiedUtc = nowUtc;

        await _jobStore.UpdateAsync(job, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Recording ingest for interaction {InteractionId} was cancelled because the recording was erased.",
                job.InteractionId.SanitizeLogValue());
        }

        return false;
    }

    private async Task RecordFailureAsync(AsteriskRecordingIngestJob job, DateTime nowUtc, string error, CancellationToken cancellationToken)
    {
        job.AttemptCount++;
        job.LastError = error;
        job.ModifiedUtc = nowUtc;

        if (job.AttemptCount >= AsteriskAriConstants.RecordingIngestMaxAttempts)
        {
            job.Status = RecordingIngestJobStatus.DeadLettered;

            _logger.LogError(
                "Recording {RecordingName} could not be ingested after {AttemptCount} attempts and was dead-lettered.",
                job.RecordingName.SanitizeLogValue(),
                job.AttemptCount);
        }
        else
        {
            job.NextAttemptUtc = nowUtc.Add(ResolveBackoff(job.AttemptCount));
        }

        await _jobStore.UpdateAsync(job, cancellationToken);
    }

    private static TimeSpan ResolveBackoff(int attemptCount)
    {
        var exponent = Math.Min(attemptCount - 1, 16);
        var seconds = (double)AsteriskAriConstants.RecordingIngestBaseBackoffSeconds * Math.Pow(2, exponent);
        var capSeconds = AsteriskAriConstants.RecordingIngestMaxBackoffMinutes * 60d;

        return TimeSpan.FromSeconds(Math.Min(seconds, capSeconds));
    }
}
