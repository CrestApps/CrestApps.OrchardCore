using System.Collections.Generic;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterMonitoringService"/>.
/// </summary>
public sealed class ContactCenterMonitoringService : IContactCenterMonitoringService
{
    private static readonly MonitorMode[] _monitorModes =
    [
        MonitorMode.Monitor,
        MonitorMode.Whisper,
        MonitorMode.Barge,
        MonitorMode.TakeOver,
    ];

    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMonitoringService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to check monitoring capabilities.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    public ContactCenterMonitoringService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<MonitorMode>> GetAvailableModesAsync(
        string interactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return [];
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return [];
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceMonitoringProvider ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return [];
        }

        return _monitorModes
            .Where(mode => provider.Capabilities.HasFlag(ResolveCapability(mode)))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> EngageAsync(string interactionId, string supervisorId, MonitorMode mode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return SupervisorEngagementResult.Failure("An interaction is required.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return SupervisorEngagementResult.Failure("The interaction could not be found.");
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var capability = ResolveCapability(mode);

        if (provider is not IContactCenterVoiceMonitoringProvider monitoringProvider ||
            !provider.Capabilities.HasFlag(capability) ||
            string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement.");
        }

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoringProvider.EngageAsync(new ContactCenterVoiceMonitoringRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = interaction.ProviderInteractionId,
                    SupervisorId = supervisorId,
                    Mode = mode,
                }, commandCancellationToken));

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                return SupervisorEngagementResult.Failure(
                    providerResult?.ErrorMessage ?? $"The voice provider did not confirm the '{mode}' engagement.");
            }

            var interactionEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.SupervisorMonitorStarted,
                InteractionId = interaction.ItemId,
                AggregateType = nameof(Interaction),
                AggregateId = interaction.ItemId,
                ActorId = supervisorId,
                SourceComponent = ContactCenterConstants.Components.RealTime,
            };

            interactionEvent.SetData(new Dictionary<string, string>
            {
                ["mode"] = mode.ToString(),
                ["supervisorId"] = supervisorId,
            });

            await _publisher.PublishAsync(interactionEvent, CancellationToken.None);

            return SupervisorEngagementResult.Success();
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown(
                $"The voice provider did not confirm the '{mode}' engagement before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown(
                $"The '{mode}' engagement was interrupted before the provider outcome could be confirmed.");
        }
    }

    private static ContactCenterVoiceProviderCapabilities ResolveCapability(MonitorMode mode)
    {
        return mode switch
        {
            MonitorMode.Monitor => ContactCenterVoiceProviderCapabilities.Monitor,
            MonitorMode.Whisper => ContactCenterVoiceProviderCapabilities.Whisper,
            MonitorMode.Barge => ContactCenterVoiceProviderCapabilities.Barge,
            MonitorMode.TakeOver => ContactCenterVoiceProviderCapabilities.TakeOver,
            _ => ContactCenterVoiceProviderCapabilities.Monitor,
        };
    }
}
