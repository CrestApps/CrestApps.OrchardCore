using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterRecordingService"/>.
/// </summary>
public sealed class ContactCenterRecordingService : IContactCenterRecordingService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly IRecordingGovernancePolicy _governancePolicy;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRecordingService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="governancePolicy">The recording governance policy that gates recording and resolves retention metadata.</param>
    /// <param name="clock">The clock used to stamp the secure-pause timestamp read by the auto-resume guard.</param>
    public ContactCenterRecordingService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        IRecordingGovernancePolicy governancePolicy,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _governancePolicy = governancePolicy;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<RecordingCommandResult> StartAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Recording, ContactCenterConstants.Events.RecordingStarted, sourceStateGuard: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RecordingCommandResult> PauseAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Paused, ContactCenterConstants.Events.RecordingPaused, previous => previous == RecordingState.Recording, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RecordingCommandResult> ResumeAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Recording, ContactCenterConstants.Events.RecordingResumed, previous => previous == RecordingState.Paused, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RecordingCommandResult> AutoResumeAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Recording, ContactCenterConstants.Events.RecordingAutoResumed, previous => previous == RecordingState.Paused, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<RecordingCommandResult> StopAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Stopped, ContactCenterConstants.Events.RecordingStopped, sourceStateGuard: null, cancellationToken);
    }

    private async Task<RecordingCommandResult> SetStateAsync(
        string interactionId,
        RecordingState state,
        string eventType,
        Func<RecordingState, bool> sourceStateGuard,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return RecordingCommandResult.Failure("An interaction is required.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.RecordingState == state)
        {
            return RecordingCommandResult.Failure("The interaction could not be found or is already in the requested recording state.");
        }

        // Enforce the legal source state for the transition so a pause can only suppress an actively-recording
        // call and a resume (agent-driven or the auto-resume safety guard) can only lift a real pause. Without
        // this guard the agent-facing resume endpoint could drive an idle call into Recording and start capture.
        if (sourceStateGuard is not null && !sourceStateGuard(interaction.RecordingState))
        {
            return RecordingCommandResult.Failure("The interaction is not in a recording state that allows the requested change.");
        }

        var previousState = interaction.RecordingState;

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceRecordingProvider recordingProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return RecordingCommandResult.Failure("The voice provider does not support recording for this interaction.");
        }

        // Recording governance gates only the transition into an actively-recording state (start and resume); a
        // pause or stop must never be blocked by policy so the tenant can always halt capture.
        RecordingGovernanceDecision governanceDecision = null;

        if (state == RecordingState.Recording)
        {
            governanceDecision = await _governancePolicy.EvaluateStartAsync(interaction, cancellationToken);

            if (!governanceDecision.Allowed)
            {
                var deniedEvent = new InteractionEvent
                {
                    EventType = ContactCenterConstants.Events.RecordingDenied,
                    InteractionId = interaction.ItemId,
                    AggregateType = nameof(Interaction),
                    AggregateId = interaction.ItemId,
                    ActorId = interaction.AgentId,
                    SourceComponent = ContactCenterConstants.Components.Interactions,
                };

                deniedEvent.SetData(new RecordingDeniedEventData
                {
                    DenyReasonCode = governanceDecision.DenyReasonCode,
                });

                await _publisher.PublishAsync(deniedEvent, CancellationToken.None);

                return RecordingCommandResult.Failure(governanceDecision.DenyReasonCode ?? "Recording was denied by policy.");
            }
        }

        ContactCenterVoiceProviderResult providerResult;

        try
        {
            providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                recordingProvider.SetRecordingStateAsync(new ContactCenterVoiceRecordingRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = interaction.ProviderInteractionId,
                    State = state,
                }, commandCancellationToken));
        }
        catch (TelephonyCommandNotAdmittedException)
        {
            // Refused before the provider was contacted: the recording state is definitely unchanged.
            return RecordingCommandResult.Failure("The recording command was refused because the application is stopping.");
        }
        catch (TimeoutException)
        {
            return RecordingCommandResult.Unknown("The recording command exceeded the server-owned timeout; its provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return RecordingCommandResult.Unknown("The recording command was interrupted after dispatch; its provider outcome is unknown.");
        }

        if (providerResult is null || (!providerResult.Succeeded && !providerResult.OutcomeUnknown))
        {
            return RecordingCommandResult.Failure("The voice provider did not apply the recording state change.");
        }

        if (providerResult.OutcomeUnknown)
        {
            return RecordingCommandResult.Unknown("The voice provider could not confirm the recording state change.");
        }

        interaction.RecordingState = state;

        // Maintain the secure-pause timestamp the auto-resume guard reads: stamp it when capture is suppressed and
        // clear it (with any pause reason) the moment capture leaves the paused state, so a resumed or stopped
        // recording is never mistaken for a pause that has outlived its window.
        if (state == RecordingState.Paused)
        {
            interaction.RecordingPausedUtc = _clock.UtcNow;
        }
        else
        {
            interaction.RecordingPausedUtc = null;
            interaction.RecordingPauseReason = null;
        }

        // Determine initial capture from the persisted pre-transition state rather than the calling entry point,
        // so invoking Start on an already paused interaction (or any resume) cannot re-stamp the capture-time
        // retention window or newly raise legal hold. Only a transition into Recording from a state that was never
        // actively capturing (None, or a fully Stopped prior session) counts as an initial capture.
        var isInitialCapture = state == RecordingState.Recording &&
            previousState != RecordingState.Recording &&
            previousState != RecordingState.Paused;

        if (isInitialCapture)
        {
            ApplyGovernanceMetadata(interaction, governanceDecision);
        }

        PersistRecordingMetadata(interaction, providerResult.Metadata);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: CancellationToken.None);

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = interaction.AgentId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        }, CancellationToken.None);

        return RecordingCommandResult.Success();
    }

    private static void ApplyGovernanceMetadata(Interaction interaction, RecordingGovernanceDecision decision)
    {
        if (decision is null)
        {
            return;
        }

        // Stamp the capture-time retention window and legal-hold flag. This runs only on the initial capture
        // transition, so a later resume can neither reset the retention deadline to a resume-relative value nor
        // raise legal hold on a recording that did not begin under one.
        interaction.RecordingRetainUntilUtc = decision.RetainUntilUtc;

        if (decision.LegalHold)
        {
            interaction.RecordingLegalHold = true;
        }
    }

    private static void PersistRecordingMetadata(Interaction interaction, IDictionary<string, string> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return;
        }

        // Persist every provider-supplied recording field onto the interaction so the durable retrieval path,
        // format, and duration survive beyond the transient provider call (closing DIST-8, which previously
        // discarded this metadata). The interaction document is tenant-scoped, so the metadata stays isolated.
        interaction.TechnicalMetadata ??= new Dictionary<string, object>();

        foreach (var entry in metadata)
        {
            interaction.TechnicalMetadata[entry.Key] = entry.Value;
        }

        // The recording reference is the canonical retrieval handle, so prefer the durable storage reference and
        // fall back to the provider recording name when only the name is reported.
        if (metadata.TryGetValue(ContactCenterConstants.RecordingMetadata.StorageReference, out var storageReference) &&
            !string.IsNullOrWhiteSpace(storageReference))
        {
            interaction.RecordingReference = storageReference;
        }
        else if (metadata.TryGetValue(ContactCenterConstants.RecordingMetadata.ProviderRecordingId, out var recordingName) &&
            !string.IsNullOrWhiteSpace(recordingName))
        {
            interaction.RecordingReference = recordingName;
        }
    }
}
