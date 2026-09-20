using CrestApps.Core.ContactCenter;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Provides the default implementation of <see cref="IAgentRecordingControlService"/>. It is the agent-facing,
/// ownership-checked, policy-gated boundary over <see cref="IContactCenterRecordingService"/> that lets an agent
/// suppress recording during a sensitive-data capture and resume it afterwards.
/// </summary>
public sealed class AgentRecordingControlService : IAgentRecordingControlService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IContactCenterMonitoringService _monitoringService;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IOptionsMonitor<ContactCenterRecordingSettings> _settings;
    private readonly IEnumerable<IContactCenterRealTimeNotifier> _realTimeNotifiers;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRecordingControlService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callControlAuthorizationService">The shared call-control authorization boundary.</param>
    /// <param name="recordingService">The recording orchestration service that applies the state change.</param>
    /// <param name="monitoringService">The monitoring service used to evict live supervisor engagements when a secure pause begins.</param>
    /// <param name="voiceProviderResolver">The voice provider resolver used to check pause capability.</param>
    /// <param name="settings">The tenant recording governance settings.</param>
    /// <param name="realTimeNotifiers">The optional real-time notifiers used to broadcast the recording state change.</param>
    /// <param name="timeProvider">The time provider used to stamp the real-time notification.</param>
    public AgentRecordingControlService(
        IInteractionManager interactionManager,
        ICallControlAuthorizationService callControlAuthorizationService,
        IContactCenterRecordingService recordingService,
        IContactCenterMonitoringService monitoringService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IOptionsMonitor<ContactCenterRecordingSettings> settings,
        IEnumerable<IContactCenterRealTimeNotifier> realTimeNotifiers,
        TimeProvider timeProvider)
    {
        _interactionManager = interactionManager;
        _callControlAuthorizationService = callControlAuthorizationService;
        _recordingService = recordingService;
        _monitoringService = monitoringService;
        _voiceProviderResolver = voiceProviderResolver;
        _settings = settings;
        _realTimeNotifiers = realTimeNotifiers;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<AgentRecordingControlResult> PauseAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(userId))
        {
            return AgentRecordingControlResult.Failure("An interaction and an agent are required.");
        }

        var settings = _settings.CurrentValue;

        if (!settings.AllowAgentSecurePause)
        {
            return AgentRecordingControlResult.Failure("Agent secure pause is not enabled for this tenant.");
        }

        var trimmedReason = reason?.Trim();

        if (settings.RequirePauseReason && string.IsNullOrEmpty(trimmedReason))
        {
            return AgentRecordingControlResult.Failure("A reason is required to pause recording.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return AgentRecordingControlResult.Failure("The interaction could not be found.");
        }

        if (!ProviderSupportsSecurePause(interaction))
        {
            return AgentRecordingControlResult.Failure("The voice provider does not support pausing recording for this interaction.");
        }

        var authorization = await AuthorizeAsync(interaction, userId, principal, cancellationToken);

        if (!authorization.Succeeded)
        {
            return AgentRecordingControlResult.Failure(authorization.FailureReason);
        }

        var result = await _recordingService.PauseAsync(interactionId, cancellationToken);

        if (!result.Succeeded)
        {
            return result.OutcomeUnknown
                ? AgentRecordingControlResult.Unknown(result.Reason)
                : AgentRecordingControlResult.Failure(result.Reason);
        }

        // Evict any supervisor who was already listening before the pause began. The EngageAsync guard only
        // blocks new engagements once the state is Paused; without this teardown a coach mid-monitor would keep
        // hearing the very sensitive-data segment the pause exists to protect. This runs first, before the
        // best-effort reason write, so a concurrency failure on the audit metadata can never skip the
        // privacy-critical eviction, and it uses a non-cancellable token so a disconnected agent request cannot
        // abandon a supervisor on the secured segment.
        await _monitoringService.ForceDisengageAllAsync(interactionId, CancellationToken.None);

        // The recording service clears any prior pause reason and stamps the pause time, but it does not carry the
        // agent's justification, so persist it here once the suppression is confirmed applied. Reloading first
        // avoids overwriting the recording service's committed state under the store's optimistic concurrency.
        if (!string.IsNullOrEmpty(trimmedReason))
        {
            var paused = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

            // Only stamp the justification when the interaction is still paused. A concurrent resume or the
            // auto-resume guard may have already lifted the pause between the recording commit and this reload,
            // in which case writing the reason back would resurrect stale pause metadata onto a live recording.
            if (paused is not null && paused.RecordingState == RecordingState.Paused)
            {
                paused.RecordingPauseReason = trimmedReason;
                await _interactionManager.UpdateAsync(paused, cancellationToken: cancellationToken);
            }
        }

        await NotifyAsync(interaction, userId, authorization.AgentId, RecordingState.Paused, cancellationToken);

        return AgentRecordingControlResult.Success(isPaused: true);
    }

    /// <inheritdoc/>
    public async Task<AgentRecordingControlResult> ResumeAsync(
        string interactionId,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(userId))
        {
            return AgentRecordingControlResult.Failure("An interaction and an agent are required.");
        }

        var settings = _settings.CurrentValue;

        if (!settings.AllowAgentSecurePause)
        {
            return AgentRecordingControlResult.Failure("Agent secure pause is not enabled for this tenant.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null)
        {
            return AgentRecordingControlResult.Failure("The interaction could not be found.");
        }

        if (!ProviderSupportsSecurePause(interaction))
        {
            return AgentRecordingControlResult.Failure("The voice provider does not support pausing recording for this interaction.");
        }

        var authorization = await AuthorizeAsync(interaction, userId, principal, cancellationToken);

        if (!authorization.Succeeded)
        {
            return AgentRecordingControlResult.Failure(authorization.FailureReason);
        }

        var result = await _recordingService.ResumeAsync(interactionId, cancellationToken);

        if (!result.Succeeded)
        {
            return result.OutcomeUnknown
                ? AgentRecordingControlResult.Unknown(result.Reason)
                : AgentRecordingControlResult.Failure(result.Reason);
        }

        await NotifyAsync(interaction, userId, authorization.AgentId, RecordingState.Recording, cancellationToken);

        return AgentRecordingControlResult.Success(isPaused: false);
    }

    private Task<CallControlAuthorizationResult> AuthorizeAsync(
        Interaction interaction,
        string userId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        return _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = principal,
            UserId = userId,
            Verb = CallControlVerb.RecordingControl,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
        }, cancellationToken);
    }

    private bool ProviderSupportsSecurePause(Interaction interaction)
    {
        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        return provider is IContactCenterVoiceRecordingProvider &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording) &&
            provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.RecordingPause) &&
            !string.IsNullOrEmpty(interaction.ProviderInteractionId);
    }

    private async Task NotifyAsync(
        Interaction interaction,
        string userId,
        string agentId,
        RecordingState state,
        CancellationToken cancellationToken)
    {
        var notifier = _realTimeNotifiers.FirstOrDefault();

        if (notifier is null)
        {
            return;
        }

        await notifier.NotifyRecordingStateChangedAsync(new RecordingStateNotification
        {
            InteractionId = interaction.ItemId,
            UserId = userId,
            AgentId = agentId,
            RecordingState = state.ToString(),
            IsSecurePauseActive = state == RecordingState.Paused,
            ServerTimeUtc = _timeProvider.GetUtcNow().UtcDateTime,
        }, cancellationToken);
    }
}
