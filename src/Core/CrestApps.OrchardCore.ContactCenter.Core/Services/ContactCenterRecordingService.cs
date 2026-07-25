using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRecordingService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="governancePolicy">The recording governance policy that gates recording and resolves retention metadata.</param>
    public ContactCenterRecordingService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        IRecordingGovernancePolicy governancePolicy)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _governancePolicy = governancePolicy;
    }

    /// <inheritdoc/>
    public Task<bool> StartAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Recording, ContactCenterConstants.Events.RecordingStarted, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> PauseAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Paused, ContactCenterConstants.Events.RecordingPaused, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> ResumeAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Recording, ContactCenterConstants.Events.RecordingResumed, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> StopAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        return SetStateAsync(interactionId, RecordingState.Stopped, ContactCenterConstants.Events.RecordingStopped, cancellationToken);
    }

    private async Task<bool> SetStateAsync(string interactionId, RecordingState state, string eventType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.RecordingState == state)
        {
            return false;
        }

        var previousState = interaction.RecordingState;

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceRecordingProvider recordingProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return false;
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

                return false;
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
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
        {
            return false;
        }

        interaction.RecordingState = state;

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

        return true;
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
        else if (metadata.TryGetValue(ContactCenterConstants.RecordingMetadata.RecordingName, out var recordingName) &&
            !string.IsNullOrWhiteSpace(recordingName))
        {
            interaction.RecordingReference = recordingName;
        }
    }
}
