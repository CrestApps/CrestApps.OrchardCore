using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore;
using OrchardCore.Modules;

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
    ];

    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterMonitoringService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager that owns the live call topology.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to check monitoring capabilities.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="commandExecutor">The executor that provides a bounded server-owned provider-operation token.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    /// <param name="clock">The clock used to stamp engagement times.</param>
    public ContactCenterMonitoringService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        ICallControlAuthorizationService callControlAuthorizationService,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _callControlAuthorizationService = callControlAuthorizationService;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _clock = clock;
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

        return ResolveAvailableModes(interaction);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<MonitorMode>> GetAvailableModesAsync(
        Interaction interaction,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<MonitorMode>>(ResolveAvailableModes(interaction));
    }

    private MonitorMode[] ResolveAvailableModes(Interaction interaction)
    {
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

        // Fail closed while recording is paused for a sensitive-data capture: a supervisor who engaged during the
        // secured segment would hear the very card or identity data the pause exists to keep out of reach, so no
        // new monitor, whisper, or barge leg may be brought up until capture resumes.
        if (interaction.RecordingState == RecordingState.Paused)
        {
            return SupervisorEngagementResult.Failure("A sensitive-data capture is in progress on this interaction. Monitoring is unavailable until it completes.");
        }

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

        var providerCallId = authorization.ProviderCallId;

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var capability = ResolveCapability(mode);

        if (provider is not IContactCenterVoiceMonitoringProvider monitoringProvider ||
            !provider.Capabilities.HasFlag(capability) ||
            string.IsNullOrEmpty(providerCallId))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement.");
        }

        var callSession = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        // A supervisor already engaged on this call would gain a second live leg the platform cannot tell
        // apart from the first, so a later stop would release an arbitrary one and leave the other listening.
        if (callSession is not null &&
            callSession.ActiveMonitorSessions.Any(monitorSession =>
                string.Equals(monitorSession.SupervisorUserId, supervisorId, StringComparison.Ordinal)))
        {
            return SupervisorEngagementResult.Failure("The supervisor is already engaged on this call.");
        }

        // Supervising one's own call is refused here rather than at persist time. The provider engage command
        // runs before the engagement is recorded, so leaving this to the store's invariant would bring up a
        // real snoop or barge channel and then throw, stranding a supervisor leg nothing can later stop.
        if (callSession is not null &&
            !string.IsNullOrEmpty(authorization.AgentId) &&
            string.Equals(authorization.AgentId, callSession.AgentId, StringComparison.Ordinal))
        {
            return SupervisorEngagementResult.Failure("A supervisor cannot engage on their own call.");
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

            await RecordEngagementStartedAsync(
                callSession,
                supervisorId,
                authorization.AgentId,
                mode,
                providerResult.ProviderLegId,
                cancellationToken);

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

        var providerCallId = authorization.ProviderCallId;

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

            await RecordEngagementStoppedAsync(interaction.ItemId, supervisorId, cancellationToken);

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

    /// <inheritdoc/>
    public async Task<int> ForceDisengageAllAsync(
        string interactionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return 0;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return 0;
        }

        var callSession = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (callSession is null)
        {
            return 0;
        }

        // Snapshot the live engagements up front: RecordEngagementStoppedAsync mutates the collection as it ends
        // each session, so enumerating the live view directly would skip entries.
        var activeSessions = callSession.ActiveMonitorSessions.ToArray();

        if (activeSessions.Length == 0)
        {
            return 0;
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);
        var providerCallId = interaction.ProviderInteractionId;
        var stopped = 0;

        foreach (var monitorSession in activeSessions)
        {
            // A supervisor leg can only be honestly declared gone when the provider confirms the stop. If the
            // provider cannot be reached, returns an unknown outcome, or the command times out, the engagement is
            // left live and un-published so the platform never reports a supervisor as removed while their media
            // leg may still be up and audible. The recording is already suppressed, so the primary privacy
            // guarantee holds; a lingering leg is reconciled by later provider events or when the call ends.
            if (provider is not IContactCenterVoiceMonitoringProvider monitoringProvider ||
                string.IsNullOrEmpty(providerCallId))
            {
                continue;
            }

            ContactCenterVoiceProviderResult providerResult;

            try
            {
                providerResult = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                    monitoringProvider.StopAsync(new ContactCenterVoiceMonitoringRequest
                    {
                        InteractionId = interaction.ItemId,
                        ProviderCallId = providerCallId,
                        SupervisorId = monitorSession.SupervisorUserId,
                        Mode = monitorSession.Mode,
                    }, commandCancellationToken));
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (OperationCanceledException)
            {
                continue;
            }

            if (providerResult?.Succeeded != true || providerResult.OutcomeUnknown)
            {
                continue;
            }

            await RecordEngagementStoppedAsync(interaction.ItemId, monitorSession.SupervisorUserId, cancellationToken);

            var interactionEvent = new InteractionEvent
            {
                EventType = ContactCenterConstants.Events.SupervisorMonitorStopped,
                InteractionId = interaction.ItemId,
                AggregateType = nameof(Interaction),
                AggregateId = interaction.ItemId,
                ActorId = monitorSession.SupervisorUserId,
                SourceComponent = ContactCenterConstants.Components.RealTime,
            };

            interactionEvent.SetData(new Dictionary<string, string>
            {
                ["mode"] = monitorSession.Mode.ToString(),
                ["supervisorId"] = monitorSession.SupervisorUserId,
                ["reason"] = "secure-pause",
            });

            await _publisher.PublishAsync(interactionEvent, CancellationToken.None);

            stopped++;
        }

        return stopped;
    }

    private async Task RecordEngagementStartedAsync(
        CallSession callSession,
        string supervisorUserId,
        string supervisorAgentId,
        MonitorMode mode,
        string providerLegId,
        CancellationToken cancellationToken)
    {
        if (callSession is null)
        {
            return;
        }

        CallTopologyProjector.StartMonitorSession(
            callSession,
            IdGenerator.GenerateId(),
            supervisorUserId,
            supervisorAgentId,
            mode,
            _clock.UtcNow,
            providerLegId);

        await _callSessionManager.UpdateAsync(callSession, cancellationToken: cancellationToken);
    }

    private async Task RecordEngagementStoppedAsync(
        string interactionId,
        string supervisorUserId,
        CancellationToken cancellationToken)
    {
        var callSession = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (callSession is null || !CallTopologyProjector.EndMonitorSession(callSession, supervisorUserId, _clock.UtcNow))
        {
            return;
        }

        await _callSessionManager.UpdateAsync(callSession, cancellationToken: cancellationToken);
    }

    private static ContactCenterVoiceProviderCapabilities ResolveCapability(MonitorMode mode)
    {
        return mode switch
        {
            MonitorMode.Monitor => ContactCenterVoiceProviderCapabilities.Monitor,
            MonitorMode.Whisper => ContactCenterVoiceProviderCapabilities.Whisper,
            MonitorMode.Barge => ContactCenterVoiceProviderCapabilities.Barge,
            _ => ContactCenterVoiceProviderCapabilities.Monitor,
        };
    }
}
