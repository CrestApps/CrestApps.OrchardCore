using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterPhoneCallSupervisionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// An agent's own phone call is found in the soft phone's call history: the number they dialed is their own call, and an
/// extension call is its caller's, with the colleague it rang read from the extension it dialed. The voice provider
/// then reads how the call is put together (<see cref="IContactCenterVoicePhoneCallMonitoringProvider"/>), and the
/// engagement runs through the provider's monitoring commands exactly as a Contact Center one does -- the supervisor's
/// own phone is told to expect its leg, rung, and answers it by itself.
/// </para>
/// <para>
/// There is no interaction or call session to record the engagement on, so it is kept in
/// <see cref="IPhoneCallEngagementStore"/> for as long as the call lasts, and the supervisor's leg reports come here
/// through <see cref="ISupervisorLegEventSink"/> when no Contact Center call claims them.
/// </para>
/// </remarks>
public sealed partial class ContactCenterPhoneCallSupervisionService : IContactCenterPhoneCallSupervisionService
{
    // How many active phone calls are read to find the agents on one.
    private const int ActiveCallScanLimit = 500;

    private static readonly MonitorMode[] _monitorModes =
    [
        MonitorMode.Monitor,
        MonitorMode.Whisper,
        MonitorMode.Barge,
    ];

    private readonly IPhoneCallEngagementStore _engagements;
    private readonly ITelephonyInteractionStore _telephonyInteractions;
    private readonly ITelephonyExtensionResolver _extensionResolver;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly ITelephonyService _telephonyService;
    private readonly ISupervisorEngagementNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterPhoneCallSupervisionService"/> class.
    /// </summary>
    public ContactCenterPhoneCallSupervisionService(
        IPhoneCallEngagementStore engagements,
        IEnumerable<ITelephonyInteractionStore> telephonyInteractions,
        IEnumerable<ITelephonyExtensionResolver> extensionResolvers,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IAgentProfileManager agentProfileManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IAuthorizationService authorizationService,
        ITelephonyCommandExecutor commandExecutor,
        ITelephonyService telephonyService,
        IEnumerable<ISupervisorEngagementNotifier> notifiers,
        IClock clock,
        ILogger<ContactCenterPhoneCallSupervisionService> logger)
    {
        _engagements = engagements;
        _telephonyInteractions = telephonyInteractions?.FirstOrDefault();
        _extensionResolver = extensionResolvers?.FirstOrDefault();
        _voiceProviderResolver = voiceProviderResolver;
        _agentProfileManager = agentProfileManager;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        _authorizationService = authorizationService;
        _commandExecutor = commandExecutor;
        _telephonyService = telephonyService;
        _notifier = notifiers?.FirstOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, AgentPhoneCall>> FindCallsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var calls = new Dictionary<string, AgentPhoneCall>(StringComparer.Ordinal);
        var wanted = new HashSet<string>((userIds ?? []).Where(userId => !string.IsNullOrEmpty(userId)), StringComparer.Ordinal);

        if (wanted.Count == 0 || _telephonyInteractions is null)
        {
            return calls;
        }

        var active = await _telephonyInteractions.GetActiveAsync(ActiveCallScanLimit, cancellationToken);
        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var call in active)
        {
            if (call is null || call.EndedUtc.HasValue || string.IsNullOrEmpty(call.CallId))
            {
                continue;
            }

            if (wanted.Contains(call.UserId ?? string.Empty) && !calls.ContainsKey(call.UserId))
            {
                calls[call.UserId] = new AgentPhoneCall
                {
                    UserId = call.UserId,
                    CallId = call.CallId,
                    ProviderName = call.ProviderName,
                    IsExtension = call.IsExtension,
                    Direction = call.Direction,
                    Party = call.Direction == CallDirection.Inbound ? call.From : call.To,
                    StartedUtc = call.StartedUtc,
                };
            }

            // An extension call is recorded for its caller only; the colleague it rang is found from the extension dialed.
            var calleeUserId = call.IsExtension ? await ResolveExtensionAsync(extensions, call.ExtensionNumber, cancellationToken) : null;

            if (!string.IsNullOrEmpty(calleeUserId) && wanted.Contains(calleeUserId) && !calls.ContainsKey(calleeUserId))
            {
                calls[calleeUserId] = new AgentPhoneCall
                {
                    UserId = calleeUserId,
                    CallId = call.CallId,
                    ProviderName = call.ProviderName,
                    IsExtension = true,
                    IsCallee = true,
                    Direction = CallDirection.Inbound,
                    Party = string.IsNullOrWhiteSpace(call.UserName) ? call.From : call.UserName,
                    StartedUtc = call.StartedUtc,
                };
            }
        }

        return calls;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<MonitorMode> GetAvailableModes(AgentPhoneCall call)
    {
        if (call is null ||
            string.IsNullOrEmpty(call.ProviderName) ||
            _voiceProviderResolver.Get(call.ProviderName) is not IContactCenterVoiceProvider provider ||
            provider is not IContactCenterVoiceMonitoringProvider ||
            provider is not IContactCenterVoicePhoneCallMonitoringProvider phoneCalls ||
            !phoneCalls.CanMonitorPhoneCall(call.IsExtension, call.Direction == CallDirection.Outbound))
        {
            return [];
        }

        return _monitorModes
            .Where(mode => provider.Capabilities.HasFlag(ResolveCapability(mode)))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthorizedAsync(ClaimsPrincipal principal, string supervisorUserId, string key, CancellationToken cancellationToken = default)
    {
        if (principal is null ||
            string.IsNullOrEmpty(supervisorUserId) ||
            !PhoneCallKey.TryParse(key, out var monitoredUserId, out _))
        {
            return false;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(monitoredUserId, cancellationToken);

        if (agent is null)
        {
            return false;
        }

        // The agents a supervisor sees on the dashboard: those working at least one queue the supervisor oversees.
        foreach (var queueId in agent.QueueIds ?? [])
        {
            if (await _supervisorQueueAuthorizationService.IsAuthorizedAsync(principal, supervisorUserId, queueId, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public Task<PhoneCallEngagement> FindEngagementAsync(string key, string supervisorUserId, CancellationToken cancellationToken = default)
        => PhoneCallKey.TryParse(key, out _, out var callId)
            ? _engagements.FindAsync(callId, supervisorUserId, cancellationToken)
            : Task.FromResult<PhoneCallEngagement>(null);

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> EngageAsync(
        string key,
        string supervisorUserId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!PhoneCallKey.TryParse(key, out var monitoredUserId, out var callId) || string.IsNullOrEmpty(supervisorUserId))
        {
            return SupervisorEngagementResult.Failure("The call could not be found.");
        }

        if (string.Equals(monitoredUserId, supervisorUserId, StringComparison.Ordinal))
        {
            return SupervisorEngagementResult.Failure("A supervisor cannot engage on their own call.");
        }

        var call = await FindCallAsync(monitoredUserId, callId, cancellationToken);

        if (call is null)
        {
            return SupervisorEngagementResult.Failure("The call has ended.");
        }

        var provider = _voiceProviderResolver.Get(call.ProviderName);

        if (provider is not IContactCenterVoiceMonitoringProvider monitoring ||
            provider is not IContactCenterVoicePhoneCallMonitoringProvider phoneCalls ||
            !GetAvailableModes(call).Contains(mode))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement on this call.");
        }

        if (await _engagements.FindAsync(callId, supervisorUserId, cancellationToken) is not null)
        {
            return SupervisorEngagementResult.Failure("The supervisor is already engaged on this call.");
        }

        var target = await phoneCalls.ResolvePhoneCallAsync(call.CallId, call.IsExtension, call.IsCallee, cancellationToken);

        if (target is null || string.IsNullOrEmpty(target.ProviderCallId) || string.IsNullOrEmpty(target.AgentLegId))
        {
            return SupervisorEngagementResult.Failure("This call cannot be monitored right now: it is not connected, or it was moved.");
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(monitoredUserId, cancellationToken);
        var engagement = new PhoneCallEngagement
        {
            CallId = call.CallId,
            ProviderName = call.ProviderName,
            MonitoredUserId = monitoredUserId,
            MonitoredAgentId = agent?.ItemId,
            SupervisorUserId = supervisorUserId,
            Mode = mode,
            MonitorToken = Guid.NewGuid().ToString("N"),
            ProviderCallId = target.ProviderCallId,
            AgentLegId = target.AgentLegId,
            OtherPartyLegId = target.OtherPartyLegId,
            ConferenceName = target.ConferenceName,
            CanTakeOver = target.CanTakeOver,
            StartedUtc = _clock.UtcNow,
        };

        await _engagements.SaveAsync(engagement, cancellationToken);

        // The supervisor's phone is told which leg to expect before it is rung, so it answers that leg by itself.
        await NotifyAsync(SupervisorEngagementNotification.Requested, engagement, agent, reason: null);

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoring.EngageAsync(Request(engagement, mode), commandCancellationToken));
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown($"The voice provider did not confirm the '{mode}' engagement before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown($"The '{mode}' engagement was interrupted before the provider outcome could be confirmed.");
        }

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            var failure = result?.ErrorMessage ?? $"The voice provider did not confirm the '{mode}' engagement.";

            await _engagements.RemoveAsync(engagement, cancellationToken);
            await NotifyAsync(SupervisorEngagementNotification.Ended, engagement, agent, failure);

            return SupervisorEngagementResult.Failure(failure);
        }

        engagement.SupervisorLegId = result.ProviderLegId;
        await _engagements.SaveAsync(engagement, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor '{SupervisorUserId}' started a {Mode} engagement on the phone call '{CallId}' of user '{MonitoredUserId}' on leg '{SupervisorLegId}'.",
                supervisorUserId.SanitizeLogValue(),
                mode,
                call.CallId.SanitizeLogValue(),
                monitoredUserId.SanitizeLogValue(),
                engagement.SupervisorLegId.SanitizeLogValue());
        }

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SwitchModeAsync(
        string key,
        string supervisorUserId,
        ClaimsPrincipal principal,
        MonitorMode mode,
        CancellationToken cancellationToken = default)
    {
        var engagement = await FindEngagementAsync(key, supervisorUserId, cancellationToken);

        if (engagement is null || engagement.TookOver)
        {
            return SupervisorEngagementResult.Failure("You are not listening to this call.");
        }

        if (engagement.Mode == mode)
        {
            return SupervisorEngagementResult.Success();
        }

        var provider = _voiceProviderResolver.Get(engagement.ProviderName);

        if (provider is not IContactCenterVoiceSupervisorInterventionProvider interventions ||
            !provider.Capabilities.HasFlag(ResolveCapability(mode)))
        {
            return SupervisorEngagementResult.Failure($"The voice provider does not support the '{mode}' engagement on this call.");
        }

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                interventions.SwitchModeAsync(Request(engagement, mode), commandCancellationToken));
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown($"The voice provider did not confirm the change to '{mode}' before the server timeout.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown($"The change to '{mode}' was interrupted before the provider outcome could be confirmed.");
        }

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            return SupervisorEngagementResult.Failure(result?.ErrorMessage ?? $"The voice provider did not confirm the change to '{mode}'.");
        }

        engagement.Mode = mode;
        await _engagements.SaveAsync(engagement, cancellationToken);
        await NotifyAsync(SupervisorEngagementNotification.ModeChanged, engagement, agent: null, reason: null);

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> StopAsync(
        string key,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var engagement = await FindEngagementAsync(key, supervisorUserId, cancellationToken);

        if (engagement is null)
        {
            return SupervisorEngagementResult.Success();
        }

        if (_voiceProviderResolver.Get(engagement.ProviderName) is not IContactCenterVoiceMonitoringProvider monitoring)
        {
            return SupervisorEngagementResult.Failure("The voice provider cannot stop the engagement.");
        }

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                monitoring.StopAsync(Request(engagement, engagement.Mode), commandCancellationToken));
        }
        catch (TimeoutException)
        {
            return SupervisorEngagementResult.Unknown("The voice provider did not confirm stopping the engagement before the server timeout; the provider outcome is unknown.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown("Stopping the engagement was interrupted before the provider outcome could be confirmed.");
        }

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            return SupervisorEngagementResult.Failure(result?.ErrorMessage ?? "The voice provider did not confirm stopping the engagement.");
        }

        await _engagements.RemoveAsync(engagement, cancellationToken);
        await NotifyAsync(SupervisorEngagementNotification.Ended, engagement, agent: null, reason: null);

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> TakeOverAsync(
        string key,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal is null || !await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.InterveneInCalls))
        {
            return SupervisorEngagementResult.Failure("You are not allowed to intervene in calls.");
        }

        var engagement = await FindEngagementAsync(key, supervisorUserId, cancellationToken);

        if (engagement is null || string.IsNullOrEmpty(engagement.SupervisorLegId))
        {
            return SupervisorEngagementResult.Failure("Listen to, coach or join the call first: a call is taken over from being on it.");
        }

        if (engagement.TookOver)
        {
            return SupervisorEngagementResult.Failure("You are already handling this call.");
        }

        if (!engagement.CanTakeOver)
        {
            return SupervisorEngagementResult.Failure("An extension call cannot be taken over: releasing either colleague ends it.");
        }

        if (!engagement.ConnectedUtc.HasValue)
        {
            return SupervisorEngagementResult.Failure("You are not connected to the call yet.");
        }

        if (_voiceProviderResolver.Get(engagement.ProviderName) is not IContactCenterVoicePhoneCallMonitoringProvider phoneCalls)
        {
            return SupervisorEngagementResult.Failure("The voice provider cannot hand this call to a supervisor.");
        }

        // The call is the supervisor's before the provider is asked: releasing the agent's leg ends the agent's call, and
        // that end comes back while the provider is still answering. Recorded afterwards, the end found a supervisor who
        // was only listening and let them go -- live, the number was left alone on the line.
        var previousMode = engagement.Mode;
        engagement.TookOver = true;
        engagement.Mode = MonitorMode.Barge;
        await _engagements.SaveAsync(engagement, cancellationToken);

        ContactCenterVoiceProviderResult result;

        try
        {
            result = await _commandExecutor.ExecuteAsync(commandCancellationToken =>
                phoneCalls.TakeOverPhoneCallAsync(Request(engagement, MonitorMode.Barge), commandCancellationToken));
        }
        catch (TimeoutException)
        {
            // The agent may already be gone: the supervisor keeps the call rather than being let go with it.
            return SupervisorEngagementResult.Unknown("The voice provider did not confirm the takeover before the server timeout.");
        }
        catch (OperationCanceledException)
        {
            return SupervisorEngagementResult.Unknown("The takeover was interrupted before the provider outcome could be confirmed.");
        }

        if (result?.Succeeded != true || result.OutcomeUnknown)
        {
            if (result?.OutcomeUnknown != true)
            {
                engagement.TookOver = false;
                engagement.Mode = previousMode;
                await _engagements.SaveAsync(engagement, CancellationToken.None);
            }

            return SupervisorEngagementResult.Failure(result?.ErrorMessage ?? "The voice provider did not confirm the takeover.");
        }
        await NotifyAsync(SupervisorEngagementNotification.TookOver, engagement, agent: null, reason: null);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Supervisor '{SupervisorUserId}' took over the phone call '{CallId}' of user '{MonitoredUserId}'.",
                supervisorUserId.SanitizeLogValue(),
                engagement.CallId.SanitizeLogValue(),
                engagement.MonitoredUserId.SanitizeLogValue());
        }

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> EndCallAsync(
        string key,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal is null || !await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.InterveneInCalls))
        {
            return SupervisorEngagementResult.Failure("You are not allowed to intervene in calls.");
        }

        if (!PhoneCallKey.TryParse(key, out _, out var callId))
        {
            return SupervisorEngagementResult.Failure("The call could not be found.");
        }

        // A call a supervisor took over is no longer the agent's: its other party is the one left to hang up.
        var takenOver = (await _engagements.ListAsync(callId, cancellationToken)).FirstOrDefault(engagement => engagement.TookOver);
        var legId = takenOver?.OtherPartyLegId ?? callId;

        TelephonyResult hangup;

        try
        {
            hangup = await _telephonyService.HangupAsync(new CallReference { CallId = legId }, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while a supervisor ended the phone call '{CallId}'.", callId.SanitizeLogValue());

            return SupervisorEngagementResult.Failure("The call could not be ended.");
        }

        return hangup?.Succeeded == true
            ? SupervisorEngagementResult.Success()
            : SupervisorEngagementResult.Failure(hangup?.Error ?? "The call could not be ended.");
    }

    private static ContactCenterVoiceMonitoringRequest Request(PhoneCallEngagement engagement, MonitorMode mode)
    {
        var request = new ContactCenterVoiceMonitoringRequest
        {
            InteractionId = PhoneCallKey.Create(engagement.MonitoredUserId, engagement.CallId),
            ProviderCallId = engagement.ProviderCallId,
            SupervisorId = engagement.SupervisorUserId,
            Mode = mode,
            AgentLegId = engagement.AgentLegId,
            SupervisorLegId = engagement.SupervisorLegId,
            MonitorToken = engagement.MonitorToken,
        };

        if (!string.IsNullOrEmpty(engagement.ConferenceName))
        {
            request.Metadata[ContactCenterPhoneCallMonitoringTarget.ConferenceMetadataKey] = engagement.ConferenceName;
        }

        return request;
    }

    private static ContactCenterVoiceProviderCapabilities ResolveCapability(MonitorMode mode)
        => mode switch
        {
            MonitorMode.Whisper => ContactCenterVoiceProviderCapabilities.Whisper,
            MonitorMode.Barge => ContactCenterVoiceProviderCapabilities.Barge,
            _ => ContactCenterVoiceProviderCapabilities.Monitor,
        };
}
