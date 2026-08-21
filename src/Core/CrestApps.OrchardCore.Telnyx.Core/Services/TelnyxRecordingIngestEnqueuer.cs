using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
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
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IContactCenterEventPublisher _eventPublisher;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;
    private readonly ILogger<TelnyxRecordingIngestEnqueuer> _logger;

    // The agent profile manager is owned by the Agents feature. Voicemail requires agents, but the recording
    // ingest itself does not, so resolve it optionally rather than taking a hard dependency on that feature.

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxRecordingIngestEnqueuer"/> class.
    /// </summary>
    /// <param name="jobStore">The durable recording ingest job store.</param>
    /// <param name="interactionManager">The interaction manager used to stamp the recording retrieval handle.</param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve a voicemail's recipient agent.</param>
    /// <param name="eventPublisher">The Contact Center event publisher used to surface a saved voicemail to its recipient.</param>
    /// <param name="clock">The clock used to stamp the job's creation and first-due time.</param>
    /// <param name="logger">The logger instance.</param>
    public TelnyxRecordingIngestEnqueuer(
        ITelnyxRecordingIngestJobStore jobStore,
        IInteractionManager interactionManager,
        IEnumerable<IAgentProfileManager> agentProfileManagers,
        IContactCenterEventPublisher eventPublisher,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<TelnyxRecordingIngestEnqueuer> logger)
    {
        _jobStore = jobStore;
        _interactionManager = interactionManager;
        _agentProfileManager = agentProfileManagers.FirstOrDefault();
        _eventPublisher = eventPublisher;
        _scopeExecutor = scopeExecutor;
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

        // A voicemail recording surfaces in the recipient agent's voicemail inbox. Flag the interaction and publish
        // the projection event once. The routing engine already flags (and projects) a call it sends to voicemail
        // before answering, so only flag here when it has not been flagged yet -- which covers an agent-initiated
        // "send to voicemail" from the soft phone, whose path does not go through the routing engine.
        var publishVoicemailProjection = false;

        if (recordingState.IsVoicemail &&
            !IsAlreadyFlaggedVoicemail(interaction))
        {
            interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = true;

            var recipientAgentId = await ResolveRecipientAgentIdAsync(recordingState.RecipientUserId, interaction, cancellationToken);

            if (!string.IsNullOrWhiteSpace(recipientAgentId))
            {
                interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.RecipientAgentMetadataKey] = recipientAgentId;
                publishVoicemailProjection = true;
            }
        }

        try
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

            if (publishVoicemailProjection)
            {
                await _eventPublisher.PublishAsync(BuildVoicemailProjectionEvent(interaction, callEvent.RecordingId), cancellationToken);
            }

            await _jobStore.EnqueueAsync(
                recordingState.InteractionId,
                callEvent.RecordingId,
                TelnyxConstants.Recording.Format,
                _clock.UtcNow,
                cancellationToken);

            // Download the recording into the encrypted media store immediately rather than waiting for the next
            // scheduled ingest sweep, so a voicemail is playable within seconds of being left instead of up to a
            // minute later. The periodic sweep remains the durable retry for any prompt attempt that fails.
            _scopeExecutor.ScheduleAfterCommit<ITelnyxRecordingIngestService>(
                ingestService => ingestService.ProcessDueAsync(CancellationToken.None));

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

    private static bool IsAlreadyFlaggedVoicemail(Interaction interaction)
    {
        return interaction.TechnicalMetadata is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.Voicemail.ProjectionMetadataKey, out var value) &&
            (value is bool boolean ? boolean : bool.TryParse(value?.ToString(), out var parsed) && parsed);
    }

    private async Task<string> ResolveRecipientAgentIdAsync(
        string recipientUserId,
        Interaction interaction,
        CancellationToken cancellationToken)
    {
        // Prefer the recipient carried on the recording (set when the call was sent to voicemail), which survives
        // even after the interaction has released its agent association; fall back to the interaction's agent.
        if (_agentProfileManager is not null && !string.IsNullOrWhiteSpace(recipientUserId))
        {
            var agent = await _agentProfileManager.FindByUserIdAsync(recipientUserId, cancellationToken);

            if (agent is not null && !string.IsNullOrWhiteSpace(agent.ItemId))
            {
                return agent.ItemId;
            }
        }

        return interaction.AgentId;
    }

    private InteractionEvent BuildVoicemailProjectionEvent(Interaction interaction, string recordingId)
    {
        return new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.CallSentToVoicemail,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            CorrelationId = interaction.CorrelationId,
            ActorId = ContactCenterConstants.SystemActor,
            SourceComponent = ContactCenterConstants.Components.Voice,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = $"voicemail-recording-{recordingId}",
        };
    }
}
