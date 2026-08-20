using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Default <see cref="ITelnyxRecordingIngestService"/> implementation. It resolves each due recording's current
/// download URL from the Telnyx recordings API, downloads it, and persists it, encrypted at rest, in the
/// pluggable media store. A recording that is not yet downloadable (or a transient store failure) is retried
/// with exponential back-off; a recording that never becomes ingestible is dead-lettered after the attempt
/// budget is exhausted so it is never retried indefinitely and is never silently lost. A recording whose
/// interaction has already had its recording erased is never (re-)ingested; any media written for it is cleaned
/// up and the job is cancelled so a late ingest cannot resurrect deleted media. Once the encrypted copy exists
/// the Telnyx-hosted recording is deleted, so no plaintext copy of the conversation lingers off-platform.
/// </summary>
internal sealed class TelnyxRecordingIngestService : ITelnyxRecordingIngestService
{
    private readonly ITelnyxRecordingIngestJobStore _jobStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IRecordingErasureGuard _erasureGuard;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxRecordingIngestService> _logger;
    private readonly TelnyxOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestService"/> class.
    /// </summary>
    /// <param name="jobStore">The durable recording ingest job store.</param>
    /// <param name="httpClientFactory">The HTTP client factory used to reach the Telnyx API and download media.</param>
    /// <param name="mediaStore">The media store that persists recordings encrypted at rest.</param>
    /// <param name="erasureGuards">
    /// The optional recording erasure guards. When the Contact Center recording governance feature is enabled the
    /// first guard is consulted to refuse ingesting a recording that has already been erased; when absent, ingest
    /// proceeds unchanged.
    /// </param>
    /// <param name="clock">The clock.</param>
    /// <param name="telnyxOptions">The Telnyx options carrying the API base address and API key.</param>
    /// <param name="logger">The logger instance.</param>
    public TelnyxRecordingIngestService(
        ITelnyxRecordingIngestJobStore jobStore,
        IHttpClientFactory httpClientFactory,
        IRecordingMediaStore mediaStore,
        IEnumerable<IRecordingErasureGuard> erasureGuards,
        IClock clock,
        IOptions<TelnyxOptions> telnyxOptions,
        ILogger<TelnyxRecordingIngestService> logger)
    {
        _jobStore = jobStore;
        _httpClientFactory = httpClientFactory;
        _mediaStore = mediaStore;
        _erasureGuard = erasureGuards.FirstOrDefault();
        _clock = clock;
        _options = telnyxOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return 0;
        }

        var nowUtc = _clock.UtcNow;
        var dueJobs = await _jobStore.GetDueAsync(nowUtc, TelnyxConstants.Recording.IngestBatchSize, cancellationToken);
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
                    "An unexpected error occurred while ingesting Telnyx recording {RecordingId}.",
                    job.RecordingId.SanitizeLogValue());

                await RecordFailureAsync(job, nowUtc, "An unexpected error occurred during ingestion.", cancellationToken);
            }
        }

        return ingested;
    }

    private async Task<bool> TryIngestAsync(TelnyxRecordingIngestJob job, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Refuse to ingest a recording whose interaction has already been erased (or no longer exists) so a late
        // job can never resurrect deleted media. This is checked before any download or store work is done.
        if (await IsRecordingErasedAsync(job.InteractionId, cancellationToken))
        {
            return await CancelErasedIngestAsync(job, nowUtc, cancellationToken);
        }

        // The encrypted store happens at most once per job. If a prior attempt already stored the recording but
        // failed to delete the Telnyx source, the retry skips straight to source cleanup instead of re-downloading
        // and re-storing the same recording.
        if (!job.MediaStored)
        {
            using var apiClient = CreateApiClient();

            var downloadUrl = await ResolveDownloadUrlAsync(apiClient, job, cancellationToken);

            // A null url means the recording is not readable yet (still finalizing) or was already removed by
            // retention. Either way the job is retried with back-off; a recording that never appears is eventually
            // dead-lettered rather than retried forever.
            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                await RecordFailureAsync(job, nowUtc, "The recording download url was not yet available from Telnyx.", cancellationToken);

                return false;
            }

            using var downloadClient = _httpClientFactory.CreateClient();

            using var mediaResponse = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!mediaResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Downloading Telnyx recording {RecordingId} returned {StatusCode}; it will be retried.",
                    job.RecordingId.SanitizeLogValue(),
                    mediaResponse.StatusCode);

                await RecordFailureAsync(job, nowUtc, "The recording could not be downloaded from Telnyx.", cancellationToken);

                return false;
            }

            // The download response is held open only for the duration of the store so the recording streams
            // straight from Telnyx into the encrypting media store without being buffered whole in memory.
            await using (var content = await mediaResponse.Content.ReadAsStreamAsync(cancellationToken))
            {
                job.MediaReference = await _mediaStore.StoreAsync(new RecordingMediaWriteRequest
                {
                    StorageKey = job.RecordingId,
                    InteractionId = job.InteractionId,
                    Format = job.Format,
                    Content = content,
                }, cancellationToken);
            }

            job.MediaStored = true;

            // Durably record that the encrypted copy exists before attempting the Telnyx source cleanup. If the
            // process crashes after a successful delete but before the job is marked Completed, the retry reloads a
            // job with MediaStored == true and skips the download/store, treating a Telnyx 404 on delete as success.
            job.ModifiedUtc = nowUtc;

            await _jobStore.UpdateAsync(job, cancellationToken);
        }

        // Re-check erasure after the media is stored: an erasure request can land during the download/store window,
        // in which case the media just written must be deleted rather than left orphaned in the store.
        if (await IsRecordingErasedAsync(job.InteractionId, cancellationToken))
        {
            return await CancelErasedIngestAsync(job, nowUtc, cancellationToken);
        }

        // The recording now lives encrypted at rest in the media store, so the Telnyx-hosted copy is deleted to
        // avoid leaving a plaintext copy of the conversation off-platform. Cleanup is part of the job lifecycle: a
        // transient delete failure is retried with back-off (without re-storing the already-durable encrypted
        // copy) rather than silently leaving the source behind.
        using (var apiClient = CreateApiClient())
        {
            if (!await DeleteTelnyxRecordingAsync(apiClient, job.RecordingId, cancellationToken))
            {
                await RecordFailureAsync(job, nowUtc, "The Telnyx recording could not be deleted after ingestion.", cancellationToken);

                return false;
            }
        }

        job.Status = TelnyxRecordingIngestJobStatus.Completed;
        job.AttemptCount++;
        job.LastError = null;
        job.ModifiedUtc = nowUtc;

        await _jobStore.UpdateAsync(job, cancellationToken);

        return true;
    }

    private async Task<string> ResolveDownloadUrlAsync(HttpClient apiClient, TelnyxRecordingIngestJob job, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await apiClient.GetAsync($"recordings/{Uri.EscapeDataString(job.RecordingId)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Telnyx returned {StatusCode} resolving the download url for recording {RecordingId}.",
                    response.StatusCode,
                    job.RecordingId.SanitizeLogValue());

                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("download_urls", out var urls) ||
                urls.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Prefer the requested format, falling back to whichever rendition Telnyx has available.
            return ReadString(urls, job.Format)
                ?? ReadString(urls, TelnyxConstants.Recording.Format)
                ?? ReadString(urls, "wav");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "The Telnyx recording metadata for {RecordingId} could not be parsed.", job.RecordingId.SanitizeLogValue());

            return null;
        }
    }

    private async Task<bool> DeleteTelnyxRecordingAsync(HttpClient apiClient, string recordingId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await apiClient.DeleteAsync($"recordings/{Uri.EscapeDataString(recordingId)}", cancellationToken);

            // Deletion is idempotent: a recording that is already absent is a confirmed delete.
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "The Telnyx recording {RecordingId} could not be deleted after ingestion; cleanup will be retried.",
                recordingId.SanitizeLogValue());

            return false;
        }
    }

    private async Task<bool> IsRecordingErasedAsync(string interactionId, CancellationToken cancellationToken)
        => _erasureGuard is not null && await _erasureGuard.IsRecordingErasedAsync(interactionId, cancellationToken);

    private async Task<bool> CancelErasedIngestAsync(TelnyxRecordingIngestJob job, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // The media store addresses recordings by their deterministic storage key (the recording id), so a crash
        // between storing the encrypted copy and persisting MediaReference/MediaStored can still be cleaned up:
        // fall back to the recording id when the reference was never durably recorded. Deletion is idempotent, so
        // an object that is already absent is treated as a confirmed delete.
        var mediaReference = string.IsNullOrEmpty(job.MediaReference) ? job.RecordingId : job.MediaReference;

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
                    "Deleting erased recording media for recording {RecordingId} failed; cancellation will be retried.",
                    job.RecordingId.SanitizeLogValue());

                await RecordFailureAsync(job, nowUtc, "Erased recording media could not be deleted during ingest cancellation.", cancellationToken);

                return false;
            }
        }

        // The Telnyx-hosted source is best-effort removed so an erased interaction leaves no copy anywhere; a
        // transient failure never blocks cancelling the job because Telnyx retention ages the recording out
        // independently.
        using (var apiClient = CreateApiClient())
        {
            await DeleteTelnyxRecordingAsync(apiClient, job.RecordingId, cancellationToken);
        }

        job.Status = TelnyxRecordingIngestJobStatus.Cancelled;
        job.AttemptCount++;
        job.LastError = "Ingest was cancelled because the recording was erased.";
        job.ModifiedUtc = nowUtc;

        await _jobStore.UpdateAsync(job, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Telnyx recording ingest for interaction {InteractionId} was cancelled because the recording was erased.",
                job.InteractionId.SanitizeLogValue());
        }

        return false;
    }

    private async Task RecordFailureAsync(TelnyxRecordingIngestJob job, DateTime nowUtc, string error, CancellationToken cancellationToken)
    {
        job.AttemptCount++;
        job.LastError = error;
        job.ModifiedUtc = nowUtc;

        if (job.AttemptCount >= TelnyxConstants.Recording.IngestMaxAttempts)
        {
            job.Status = TelnyxRecordingIngestJobStatus.DeadLettered;

            _logger.LogError(
                "Telnyx recording {RecordingId} could not be ingested after {AttemptCount} attempts and was dead-lettered.",
                job.RecordingId.SanitizeLogValue(),
                job.AttemptCount);
        }
        else
        {
            job.NextAttemptUtc = nowUtc.Add(ResolveBackoff(job.AttemptCount));
        }

        await _jobStore.UpdateAsync(job, cancellationToken);
    }

    private HttpClient CreateApiClient()
    {
        var client = _httpClientFactory.CreateClient(TelnyxConstants.ProviderTechnicalName);
        client.BaseAddress = new Uri(_options.ApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        return client;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static TimeSpan ResolveBackoff(int attemptCount)
    {
        var exponent = Math.Min(attemptCount - 1, 16);
        var seconds = (double)TelnyxConstants.Recording.IngestBaseBackoffSeconds * Math.Pow(2, exponent);
        var capSeconds = TelnyxConstants.Recording.IngestMaxBackoffMinutes * 60d;

        return TimeSpan.FromSeconds(Math.Min(seconds, capSeconds));
    }
}
