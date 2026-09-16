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
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsultTransferService"/> class.
    /// </summary>
    public ConsultTransferService(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IClock clock,
        ILogger<ConsultTransferService> logger)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
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

        var transferProvider = ResolveTransferProvider();

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
            BuildRequest(session, consultId, request.TargetAddress, providerLegId: null),
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

        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Connected, _clock.UtcNow);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

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

        var transferProvider = ResolveTransferProvider();

        if (transferProvider is null)
        {
            return false;
        }

        var result = await transferProvider.CompleteConsultAsync(
            BuildRequest(session, consult.ConsultId, consult.TargetAddress, consult.ProviderLegId),
            cancellationToken);

        if (!result.Succeeded)
        {
            return false;
        }

        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Completed, _clock.UtcNow);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> CancelAsync(string callSessionId, string consultId, CancellationToken cancellationToken = default)
    {
        var (session, consult) = await FindAsync(callSessionId, consultId, cancellationToken);

        if (consult is null || !IsLive(consult))
        {
            return false;
        }

        var transferProvider = ResolveTransferProvider();

        if (transferProvider is null)
        {
            return false;
        }

        var result = await transferProvider.CancelConsultAsync(
            BuildRequest(session, consult.ConsultId, consult.TargetAddress, consult.ProviderLegId),
            cancellationToken);

        if (!result.Succeeded)
        {
            return false;
        }

        // Cancelling must leave the customer with the agent they already had rather than in limbo; that is the
        // whole reason for consulting before committing.
        CallTopologyProjector.AdvanceConsult(session, consultId, ConsultCallStatus.Cancelled, _clock.UtcNow);

        await _callSessionManager.UpdateAsync(session, cancellationToken: cancellationToken);

        return true;
    }

    private IContactCenterVoiceAttendedTransferProvider ResolveTransferProvider()
        => _voiceProviderResolver.Get() as IContactCenterVoiceAttendedTransferProvider;

    private async Task<(CallSession Session, ConsultCall Consult)> FindAsync(string callSessionId, string consultId, CancellationToken cancellationToken)
    {
        var session = await _callSessionManager.FindByIdAsync(callSessionId, cancellationToken);

        var consult = session?.Consults
            .FirstOrDefault(candidate => string.Equals(candidate.ConsultId, consultId, StringComparison.Ordinal));

        return (session, consult);
    }

    private static ContactCenterVoiceAttendedTransferRequest BuildRequest(
        CallSession session,
        string consultId,
        string targetAddress,
        string providerLegId)
    {
        return new ContactCenterVoiceAttendedTransferRequest
        {
            InteractionId = session.InteractionId,
            ProviderCallId = session.ProviderCallId,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["consultId"] = consultId,
                ["consultLegId"] = providerLegId ?? string.Empty,
                ["targetAddress"] = targetAddress ?? string.Empty,
            },
        };
    }

    private static bool IsLive(ConsultCall consult)
        => consult.Status is ConsultCallStatus.Initiated or ConsultCallStatus.Ringing or ConsultCallStatus.Connected;
}
