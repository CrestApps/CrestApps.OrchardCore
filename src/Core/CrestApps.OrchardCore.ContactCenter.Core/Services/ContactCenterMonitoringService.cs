using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMonitoringService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to check monitoring capabilities.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    public ContactCenterMonitoringService(
        IInteractionManager interactionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        ICallControlAuthorizationService callControlAuthorizationService = null)
    {
        _interactionManager = interactionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _callControlAuthorizationService = callControlAuthorizationService;
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
    public async Task<SupervisorEngagementResult> EngageAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
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

        var providerCallId = interaction.ProviderInteractionId;

        if (_callControlAuthorizationService is not null)
        {
            var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
            {
                Principal = principal,
                UserId = supervisorId,
                Verb = CallControlVerb.SupervisorEngage,
                InteractionId = interaction.ItemId,
                ProviderName = interaction.ProviderName,
                ProviderCallId = interaction.ProviderInteractionId,
                SupervisorOperation = true,
            }, cancellationToken);

            if (!authorization.Succeeded)
            {
                return SupervisorEngagementResult.Failure(authorization.FailureReason);
            }

            providerCallId = authorization.ProviderCallId;
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var capability = ResolveCapability(mode);

        if (provider is not IContactCenterVoiceMonitoringProvider monitoringProvider ||
            !provider.Capabilities.HasFlag(capability) ||
            string.IsNullOrEmpty(providerCallId))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement.");
        }

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoringProvider.EngageAsync(new ContactCenterVoiceMonitoringRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = providerCallId,
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

    /// <summary>
    /// Engages a live interaction as a supervisor using the requested mode when the provider supports it.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="supervisorId">The supervisor performing the engagement.</param>
    /// <param name="mode">The engagement mode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The engagement result.</returns>
    public Task<SupervisorEngagementResult> EngageAsync(
        string interactionId,
        string supervisorId,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
    {
        return EngageAsync(interactionId, supervisorId, null, mode, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> StopEngagementAsync(
        string interactionId,
        string supervisorId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
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

        var providerCallId = interaction.ProviderInteractionId;

        if (_callControlAuthorizationService is not null)
        {
            var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
            {
                Principal = principal,
                UserId = supervisorId,
                Verb = CallControlVerb.SupervisorEngage,
                InteractionId = interaction.ItemId,
                ProviderName = interaction.ProviderName,
                ProviderCallId = interaction.ProviderInteractionId,
                SupervisorOperation = true,
            }, cancellationToken);

            if (!authorization.Succeeded)
            {
                return SupervisorEngagementResult.Failure(authorization.FailureReason);
            }

            providerCallId = authorization.ProviderCallId;
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceMonitoringProvider monitoringProvider ||
            string.IsNullOrEmpty(providerCallId))
        {
            return SupervisorEngagementResult.Failure($"The voice provider cannot stop the '{mode}' engagement.");
        }

        try
        {
            var providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoringProvider.StopAsync(new ContactCenterVoiceMonitoringRequest
                {
                    InteractionId = interaction.ItemId,
                    ProviderCallId = providerCallId,
                    SupervisorId = supervisorId,
                    Mode = mode,
                }, commandCancellationToken));

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                return SupervisorEngagementResult.Failure(
                    providerResult?.ErrorMessage ?? $"The voice provider did not confirm stopping the '{mode}' engagement.");
            }

            var interactionEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.SupervisorMonitorStopped,
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
                $"The voice provider did not confirm stopping the '{mode}' engagement before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown(
                $"Stopping the '{mode}' engagement was interrupted before the provider outcome could be confirmed.");
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
