using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterRecordingService"/>.
/// </summary>
public sealed class ContactCenterRecordingService : IContactCenterRecordingService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRecordingService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    public ContactCenterRecordingService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
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

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceRecordingProvider recordingProvider ||
            !provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return false;
        }

        var providerResult = await recordingProvider.SetRecordingStateAsync(new ContactCenterVoiceRecordingRequest
        {
            InteractionId = interaction.ItemId,
            ProviderCallId = interaction.ProviderInteractionId,
            State = state,
        }, cancellationToken);

        if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
        {
            return false;
        }

        interaction.RecordingState = state;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        await _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = interaction.AgentId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        }, cancellationToken);

        return true;
    }
}
