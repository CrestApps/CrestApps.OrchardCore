using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The Contact Center handler for a finished Telnyx recording: it correlates the recording to the interaction
/// that owns it (through the recording <c>client_state</c> the platform set when recording started), stamps the
/// interaction with the recording's retrieval handle, and enqueues a durable ingest job that downloads the
/// recording into the encrypted media store. Enqueueing is idempotent per recording, so a redelivered
/// saved-recording webhook never creates a duplicate job.
/// </summary>
public sealed class TelnyxRecordingIngestEnqueuer : ITelnyxRecordingSavedHandler
{
    private readonly ITelnyxRecordingIngestJobStore _jobStore;
    private readonly IInteractionManager _interactionManager;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxRecordingIngestEnqueuer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestEnqueuer"/> class.
    /// </summary>
    /// <param name="jobStore">The durable recording ingest job store.</param>
    /// <param name="interactionManager">The interaction manager used to stamp the recording retrieval handle.</param>
    /// <param name="clock">The clock used to stamp the job's creation and first-due time.</param>
    /// <param name="logger">The logger instance.</param>
    public TelnyxRecordingIngestEnqueuer(
        ITelnyxRecordingIngestJobStore jobStore,
        IInteractionManager interactionManager,
        IClock clock,
        ILogger<TelnyxRecordingIngestEnqueuer> logger)
    {
        _jobStore = jobStore;
        _interactionManager = interactionManager;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(TelnyxCallEvent callEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        if (string.IsNullOrWhiteSpace(callEvent.RecordingId))
        {
            return false;
        }

        // A recording is correlated to its interaction only through the recording client_state the platform set
        // when it started recording. A recording without that state was not started for a Contact Center
        // interaction (for example, a connection-level recording), so there is no interaction to ingest it for.
        if (!TelnyxRecordingClientState.TryParse(callEvent.ClientState, out var recordingState))
        {
            return false;
        }

        // Stamp the interaction with the recording's retrieval handle now that the Telnyx recording id is known.
        // The recording id is the deterministic media-store storage key, so recording it here makes the recording
        // discoverable through the interaction the moment the encrypted copy lands, even though ingestion itself
        // runs asynchronously. A missing interaction means there is nothing to ingest the recording for.
        var interaction = await _interactionManager.FindByIdAsync(recordingState.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return false;
        }

        interaction.RecordingReference = callEvent.RecordingId;
        interaction.TechnicalMetadata ??= new Dictionary<string, object>();
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.ProviderRecordingId] = callEvent.RecordingId;
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.StorageReference] = callEvent.RecordingId;
        interaction.TechnicalMetadata[ContactCenterConstants.RecordingMetadata.Format] = TelnyxConstants.Recording.Format;

        try
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            await _jobStore.EnqueueAsync(
                recordingState.InteractionId,
                callEvent.RecordingId,
                TelnyxConstants.Recording.Format,
                _clock.UtcNow,
                cancellationToken);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to enqueue Telnyx recording {RecordingId} for ingestion.",
                callEvent.RecordingId.SanitizeLogValue());

            return false;
        }
    }
}
