using System.Security.Claims;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using TransferRequest = CrestApps.OrchardCore.ContactCenter.Core.Models.TransferRequest;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterSupervisorInterventionService"/>.
/// </summary>
/// <remarks>
/// Every intervention on a call is authorized the way a supervisor engagement is -- the supervisor's queue scope, through
/// the shared call-control boundary with <c>SupervisorOperation</c> -- plus
/// <see cref="ContactCenterPermissions.InterveneInCalls"/>, since each one changes the call rather than observing it.
/// Each is recorded under the supervisor's name.
/// </remarks>
public sealed partial class ContactCenterSupervisorInterventionService : IContactCenterSupervisorInterventionService
{
    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ICallControlAuthorizationService _callControlAuthorizationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IContactCenterMonitoringService _monitoringService;
    private readonly IContactCenterTransferService _transferService;
    private readonly IContactCenterRecordingService _recordingService;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly ITelephonyService _telephonyService;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyCommandExecutor _commandExecutor;
    private readonly ISupervisorEngagementNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSupervisorInterventionService"/> class.
    /// </summary>
    public ContactCenterSupervisorInterventionService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ICallControlAuthorizationService callControlAuthorizationService,
        IAuthorizationService authorizationService,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IAgentProfileManager agentProfileManager,
        IAgentPresenceManager presenceManager,
        IContactCenterMonitoringService monitoringService,
        IContactCenterTransferService transferService,
        IEnumerable<IContactCenterRecordingService> recordingServices,
        IProviderVoiceEventService providerVoiceEventService,
        ITelephonyService telephonyService,
        IContactCenterAuditRecorder auditRecorder,
        IContactCenterEventPublisher publisher,
        ITelephonyCommandExecutor commandExecutor,
        IEnumerable<ISupervisorEngagementNotifier> notifiers,
        IClock clock,
        ILogger<ContactCenterSupervisorInterventionService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _callControlAuthorizationService = callControlAuthorizationService;
        _authorizationService = authorizationService;
        _supervisorQueueAuthorizationService = supervisorQueueAuthorizationService;
        _agentProfileManager = agentProfileManager;
        _presenceManager = presenceManager;
        _monitoringService = monitoringService;
        _transferService = transferService;
        _recordingService = recordingServices?.FirstOrDefault();
        _providerVoiceEventService = providerVoiceEventService;
        _telephonyService = telephonyService;
        _auditRecorder = auditRecorder;
        _publisher = publisher;
        _commandExecutor = commandExecutor;
        _notifier = notifiers?.FirstOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> EndCallAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var call = await AuthorizeCallAsync(interactionId, supervisorUserId, principal, cancellationToken);

        if (call.Failure is not null)
        {
            return call.Failure;
        }

        var interaction = call.Interaction;
        var providerCallId = call.Authorization.ProviderCallId ?? interaction.ProviderInteractionId;

        TelephonyResult hangup;

        try
        {
            hangup = await _telephonyService.HangupAsync(new CallReference { CallId = providerCallId }, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while a supervisor ended call '{ProviderCallId}'.", providerCallId.SanitizeLogValue());

            return SupervisorEngagementResult.Failure("The call could not be ended.");
        }

        if (hangup?.Succeeded != true)
        {
            return SupervisorEngagementResult.Failure(hangup?.Error ?? "The call could not be ended.");
        }

        var now = _clock.UtcNow;

        // The call ends through provider truth like any other ending, attributed to the supervisor, so its talk time,
        // the agent's wrap-up and the supervisors' release are decided where every call's are.
        await _providerVoiceEventService.IngestAsync(new ProviderVoiceEvent
        {
            ProviderName = interaction.ProviderName,
            ProviderCallId = providerCallId,
            State = VoiceCallState.Ended,
            HangupCause = HangupCause.NormalClearing,
            OccurredUtc = now,
            IdempotencyKey = $"supervisor-ended:{interaction.ItemId}",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ContactCenterConstants.TelephonyMetadata.HangupSource] = SupervisorHangupSource,
            },
        }, CancellationToken.None);

        var data = await CallDataAsync(interaction, cancellationToken);
        data.HangupSource = SupervisorHangupSource;
        data.Reason = SupervisorHangupSource;

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorEndedCall,
            data,
            now,
            ContactCenterActor.Supervisor(supervisorUserId),
            $"supervisor-ended:{interaction.ItemId}",
            CancellationToken.None);

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> TransferAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        InteractionTransferTargetType targetType,
        string targetId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return SupervisorEngagementResult.Failure("Choose where to transfer the call.");
        }

        var call = await AuthorizeCallAsync(interactionId, supervisorUserId, principal, cancellationToken);

        if (call.Failure is not null)
        {
            return call.Failure;
        }

        var interaction = call.Interaction;

        // Nobody listening can follow the call where it goes, so every engagement ends before it moves; the call is on
        // its own bridge again when the transfer runs.
        await _monitoringService.ForceDisengageAllAsync(interaction.ItemId, "transfer", CancellationToken.None);

        var result = await _transferService.TransferAsync(new TransferRequest
        {
            InteractionId = interaction.ItemId,
            Type = InteractionTransferType.Blind,
            TargetType = targetType,
            TargetId = targetId.Trim(),
            InitiatedByUserId = supervisorUserId,
            InitiatedByAgentId = call.Authorization.CallSession?.AgentId ?? interaction.AgentId,
            Principal = principal,
            SupervisorOperation = true,
        }, CancellationToken.None);

        if (!result.Succeeded)
        {
            return result.OutcomeUnknown
                ? SupervisorEngagementResult.Unknown(result.Reason)
                : SupervisorEngagementResult.Failure(result.Reason ?? "The call could not be transferred.");
        }

        var data = await CallDataAsync(interaction, cancellationToken);
        data.Target = $"{targetType}:{targetId.Trim()}";

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorTransferredCall,
            data,
            _clock.UtcNow,
            ContactCenterActor.Supervisor(supervisorUserId),
            idempotencyKey: null,
            CancellationToken.None);

        return SupervisorEngagementResult.Success();
    }

    /// <inheritdoc/>
    public async Task<SupervisorEngagementResult> SetRecordingAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        bool record,
        CancellationToken cancellationToken = default)
    {
        var call = await AuthorizeCallAsync(interactionId, supervisorUserId, principal, cancellationToken);

        if (call.Failure is not null)
        {
            return call.Failure;
        }

        var interaction = call.Interaction;

        // A pause is a sensitive-data capture the agent is running; turning recording on or off in the middle of it
        // would either capture the data or end the pause's own guarantee.
        if (interaction.RecordingState == RecordingState.Paused)
        {
            return SupervisorEngagementResult.Failure(SensitiveCaptureMessage);
        }

        if (record == (interaction.RecordingState == RecordingState.Recording))
        {
            return SupervisorEngagementResult.Success();
        }

        if (_recordingService is null)
        {
            return SupervisorEngagementResult.Failure("Call recording is not enabled.");
        }

        var result = record
            ? await _recordingService.StartAsync(interaction.ItemId, CancellationToken.None)
            : await _recordingService.StopAsync(interaction.ItemId, CancellationToken.None);

        if (!result.Succeeded)
        {
            return result.OutcomeUnknown
                ? SupervisorEngagementResult.Unknown(result.Reason)
                : SupervisorEngagementResult.Failure(result.Reason ?? "The recording could not be changed.");
        }

        var data = await CallDataAsync(interaction, cancellationToken);
        data.State = record ? nameof(RecordingState.Recording) : nameof(RecordingState.Stopped);

        await _auditRecorder.RecordCallAsync(
            ContactCenterConstants.Events.SupervisorChangedRecording,
            data,
            _clock.UtcNow,
            ContactCenterActor.Supervisor(supervisorUserId),
            idempotencyKey: null,
            CancellationToken.None);

        return SupervisorEngagementResult.Success();
    }

    private const string SupervisorHangupSource = "supervisor";

    private const string SensitiveCaptureMessage = "A sensitive-data capture is in progress on this interaction. Try again when it completes.";

    private async Task<CallAuthorization> AuthorizeCallAsync(
        string interactionId,
        string supervisorUserId,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId) || string.IsNullOrEmpty(supervisorUserId))
        {
            return CallAuthorization.Refused("An interaction and a supervisor are required.");
        }

        if (principal is null || !await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.InterveneInCalls))
        {
            return CallAuthorization.Refused("You are not allowed to intervene in calls.");
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.IsSettled)
        {
            return CallAuthorization.Refused("The call has ended.");
        }

        var authorization = await _callControlAuthorizationService.AuthorizeAsync(new CallControlAuthorizationContext
        {
            Principal = principal,
            UserId = supervisorUserId,
            Verb = CallControlVerb.SupervisorEngage,
            InteractionId = interaction.ItemId,
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            SupervisorOperation = true,
        }, cancellationToken);

        return authorization.Succeeded
            ? new CallAuthorization(null, interaction, authorization)
            : CallAuthorization.Refused(authorization.FailureReason ?? "The call is not available.");
    }

    private async Task<CallLifecycleEventData> CallDataAsync(Interaction interaction, CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        return session is null
            ? ContactCenterCallAudit.ForInteraction(interaction)
            : ContactCenterCallAudit.ForSession(session, interaction);
    }

    private sealed record CallAuthorization(
        SupervisorEngagementResult Failure,
        Interaction Interaction,
        CallControlAuthorizationResult Authorization)
    {
        public static CallAuthorization Refused(string reason)
            => new(SupervisorEngagementResult.Failure(reason), null, null);
    }
}
