using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Drives the three phases of an attended transfer against the call session, so the consult is a fact the
/// platform records rather than provider state nobody can see.
/// <para>
/// The consult model existed and nothing drove it, so agents had blind transfer and nothing else: the customer
/// was dropped on somebody who had not agreed to take them, and if that person did not answer the customer was
/// simply gone. Recording each phase is also what lets a supervisor see that a customer is on hold while their
/// agent talks to someone else, and lets reporting tell a completed warm transfer from an abandoned consult.
/// </para>
/// </summary>
public sealed class ConsultTransferService : IConsultTransferService
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IContactCenterAuditRecorder _auditRecorder;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsultTransferService"/> class.
    /// </summary>
    public ConsultTransferService(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IContactCenterAuditRecorder auditRecorder,
        IClock clock,
        ILogger<ConsultTransferService> logger)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _auditRecorder = auditRecorder;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConsultCall> StartAsync(ConsultTransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _callSessionManager.FindByIdAsync(request.CallSessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        // An agent cannot be in two private conversations at once, and a second consult would leave the first
        // destination talking to nobody while still believing they are on a call.
        if (session.Consults.Any(IsLive))
        {
            return null;
        }

        var transferProvider = ResolveTransferProvider(session);

        // A provider that cannot hold a customer and ring a third party privately cannot do a warm transfer, and
        // the agent needs to be told that rather than watching a consult that never happens.
        if (transferProvider is null)
        {
            _logger.LogWarning("The active voice provider does not support attended transfer, so no consult was placed.");

            return null;
        }

        var now = _clock.UtcNow;
        var consultId = IdGenerator.GenerateId();

        var result = await transferProvider.BeginConsultAsync(
            BuildRequest(session, consultId, request.TargetAddress, providerLegId: null, request.InitiatedByAgentId, request.Metadata),
            cancellationToken);

        // The provider refuses a destination the platform will not place a call to, so a consult cannot be a way
        // around the destination catalog. Recording one that never started would show a supervisor a customer on
        // hold for a conversation that is not happening, which is why the command runs before the projection.
        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "The consult for call '{CallSessionId}' was not started by the provider: {ErrorCode} {ErrorMessage}.",
                request.CallSessionId.SanitizeLogValue(),
                result.ErrorCode.SanitizeLogValue(),
                result.ErrorMessage.SanitizeLogValue());

            return null;
        }

        // The projector is the only writer of live call topology, so the consult and its leg are recorded
        // through it rather than by appending to the session here.
        var consult = CallTopologyProjector.StartConsult(
            session,
            consultId,
            request.InitiatedByAgentId,
            request.TargetType,
            request.TargetId,
            request.TargetAddress,
            now,
            result.ProviderLegId);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _auditRecorder.RecordConsultAsync(ContactCenterConstants.Events.ConsultStarted, session, consult, now, cancellationToken);

        return consult;
    }

    /// <inheritdoc/>
    public async Task<bool> MarkConnectedAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default)
    {
        var (session, consult) = await FindAsync(callSessionId, consultId, cancellationToken);

        if (consult is null || !IsLive(consult))
        {
            return false;
        }

        var connectedUtc = _clock.UtcNow;
        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Connected, connectedUtc);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _auditRecorder.RecordConsultAsync(ContactCenterConstants.Events.ConsultConnected, session, consult, connectedUtc, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default)
    {
        var (session, consult) = await FindAsync(callSessionId, consultId, cancellationToken);

        // Only a connected consult may be completed. Completing one nobody answered hands the customer to a
        // ringing phone and hangs up on them if it is never picked up; the agent has to take the customer back.
        // The same check makes a double-clicked button or a redelivered command place one transfer, not two.
        if (consult is null || consult.Status != ConsultCallStatus.Connected)
        {
            return false;
        }

        var transferProvider = ResolveTransferProvider(session);

        if (transferProvider is null)
        {
            return false;
        }

        var result = await transferProvider.CompleteConsultAsync(
            BuildRequest(session, consult.ConsultId, consult.TargetAddress, consult.ProviderLegId, consult.InitiatedByAgentId, BuildTargetMetadata(consult)),
            cancellationToken);

        if (!result.Succeeded)
        {
            return false;
        }

        var completedUtc = _clock.UtcNow;
        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Completed, completedUtc);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _auditRecorder.RecordConsultAsync(ContactCenterConstants.Events.ConsultCompleted, session, consult, completedUtc, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public Task<bool> CancelAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default)
        => CancelCoreAsync(callSessionId, consultId, ConsultEndedBy.Agent, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> EndByTargetAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default)
        => CancelCoreAsync(callSessionId, consultId, ConsultEndedBy.Target, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> EndForCallerHangupAsync(string callSessionId, CancellationToken cancellationToken = default)
    {
        var session = await _callSessionManager.FindByIdAsync(callSessionId, cancellationToken);

        if (session is null)
        {
            return 0;
        }

        var ended = 0;
        var liveConsultIds = session.Consults.Where(IsLive).Select(consult => consult.ConsultId).ToArray();

        foreach (var consultId in liveConsultIds)
        {
            if (await CancelCoreAsync(callSessionId, consultId, ConsultEndedBy.Caller, cancellationToken))
            {
                ended++;
            }
        }

        return ended;
    }

    private async Task<bool> CancelCoreAsync(string callSessionId, string consultId, ConsultEndedBy endedBy, CancellationToken cancellationToken)
    {
        var (session, consult) = await FindAsync(callSessionId, consultId, cancellationToken);

        if (consult is null || !IsLive(consult))
        {
            return false;
        }

        var transferProvider = ResolveTransferProvider(session);

        if (transferProvider is null)
        {
            return false;
        }

        var metadata = BuildTargetMetadata(consult);

        // What is left to undo depends on who left. The agent cancelling drops the destination and brings the
        // customer back; a destination that hung up is already gone, so only the customer comes back; a customer who
        // hung up is already gone, so only the destination is dropped.
        metadata[ContactCenterConstants.AttendedTransferMetadata.EndedBy] = endedBy switch
        {
            ConsultEndedBy.Target => ContactCenterConstants.AttendedTransferMetadata.EndedByTarget,
            ConsultEndedBy.Caller => ContactCenterConstants.AttendedTransferMetadata.EndedByCaller,
            _ => ContactCenterConstants.AttendedTransferMetadata.EndedByAgent,
        };

        var result = await transferProvider.CancelConsultAsync(
            BuildRequest(session, consult.ConsultId, consult.TargetAddress, consult.ProviderLegId, consult.InitiatedByAgentId, metadata),
            cancellationToken);

        // A consult whose caller or destination is already gone is over whatever the provider says about the rest of
        // the teardown; recording it as still live would keep a supervisor watching a conversation nobody is in.
        if (!result.Succeeded && endedBy == ConsultEndedBy.Agent)
        {
            return false;
        }

        // Cancelling must leave the customer with the agent they already had rather than in limbo; that is the
        // whole reason for consulting before committing.
        var cancelledUtc = _clock.UtcNow;
        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Cancelled, cancelledUtc);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);
        await _auditRecorder.RecordConsultAsync(ContactCenterConstants.Events.ConsultCancelled, session, consult, cancelledUtc, cancellationToken);

        return true;
    }

    private IContactCenterVoiceAttendedTransferProvider ResolveTransferProvider(CallSession session)
        => _voiceProviderResolver.Get(session.ProviderName) as IContactCenterVoiceAttendedTransferProvider;

    private async Task<(CallSession Session, ConsultCall Consult)> FindAsync(string callSessionId, string consultId, CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByIdAsync(callSessionId, cancellationToken);

        var consult = session?.Consults
            .FirstOrDefault(candidate => string.Equals(candidate.ConsultId, consultId, StringComparison.Ordinal));

        return (session, consult);
    }

    private static Dictionary<string, string> BuildTargetMetadata(ConsultCall consult)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = consult.TargetType.ToString(),
        };

    private static ContactCenterVoiceAttendedTransferRequest BuildRequest(
        CallSession session,
        string consultId,
        string targetAddress,
        string providerLegId,
        string initiatedByAgentId,
        IDictionary<string, string> extraMetadata)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["consultId"] = consultId,
            ["consultLegId"] = providerLegId ?? string.Empty,
            ["targetAddress"] = targetAddress ?? string.Empty,
        };

        // The provider has to reach the consulting agent's own leg -- to move it next to the destination, and to
        // drop it on completion -- and only the topology knows which leg that is.
        var agentLegId = FindAgentLegId(session, initiatedByAgentId);

        if (!string.IsNullOrEmpty(agentLegId))
        {
            metadata[ContactCenterConstants.AttendedTransferMetadata.AgentLegId] = agentLegId;
        }

        if (extraMetadata is not null)
        {
            foreach (var entry in extraMetadata)
            {
                metadata[entry.Key] = entry.Value ?? string.Empty;
            }
        }

        return new ContactCenterVoiceAttendedTransferRequest
        {
            InteractionId = session.InteractionId,
            ProviderCallId = session.ProviderCallId,
            Metadata = metadata,
        };
    }

    private static string FindAgentLegId(CallSession session, string agentId)
        => session.Legs
            .Where(leg =>
                leg is not null &&
                leg.Role == CallPartyRole.Agent &&
                !leg.EndedUtc.HasValue &&
                !string.IsNullOrWhiteSpace(leg.ProviderLegId) &&
                !string.Equals(leg.ProviderLegId, session.ProviderCallId, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(leg.AgentId) || string.Equals(leg.AgentId, agentId, StringComparison.Ordinal)))
            .Select(leg => leg.ProviderLegId)
            .LastOrDefault();

    private static bool IsLive(ConsultCall consult)
        => consult.Status is ConsultCallStatus.Initiated or ConsultCallStatus.Ringing or ConsultCallStatus.Connected;

    private enum ConsultEndedBy
    {
        Agent,
        Target,
        Caller,
    }
}
