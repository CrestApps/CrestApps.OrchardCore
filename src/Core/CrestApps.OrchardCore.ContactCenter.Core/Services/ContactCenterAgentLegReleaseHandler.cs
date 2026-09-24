using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Hangs up the agent legs a call still had when it ended, so the agent is not left on a line the caller has left.
/// </summary>
/// <remarks>
/// A provider that reaches the agent through a leg of its own and bridges it to the caller does not always tear that
/// leg down when the caller hangs up. Live, the agent's leg stayed up for another half minute after the caller's had
/// gone. When a call ends, every agent leg that was answered and ended with the call (rather than by hanging up itself,
/// which records its own cause) is handed to the provider to release. Releasing a leg that is already gone is not an
/// error, so a redelivered event does nothing new.
/// </remarks>
public sealed class ContactCenterAgentLegReleaseHandler : IContactCenterEventHandler
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentLegReleaseHandler"/> class.
    /// </summary>
    /// <param name="callSessionManager">The call session manager used to read the ended call's legs.</param>
    /// <param name="voiceProviderResolver">The resolver used to reach the provider that owns the call.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterAgentLegReleaseHandler(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ILogger<ContactCenterAgentLegReleaseHandler> logger)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/AgentLegRelease/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.CallEnded)
        {
            return;
        }

        var interactionId = string.IsNullOrEmpty(interactionEvent.InteractionId)
            ? interactionEvent.AggregateId
            : interactionEvent.InteractionId;

        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var session = await _callSessionManager.FindByInteractionIdAsync(interactionId, cancellationToken);

        if (session is null ||
            !CallSessionLifecycle.IsTerminal(session.State) ||
            string.IsNullOrWhiteSpace(session.ProviderName) ||
            _voiceProviderResolver.Get(session.ProviderName) is not IContactCenterVoiceAgentLegReleaseProvider provider)
        {
            return;
        }

        foreach (var agentLegId in AgentLegsEndedWithTheCall(session))
        {
            await provider.ReleaseAgentLegAsync(agentLegId, cancellationToken);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Released agent leg '{AgentLegId}' of ended call session '{CallSessionId}'.",
                    agentLegId.SanitizeLogValue(),
                    session.ItemId.SanitizeLogValue());
            }
        }
    }

    private static string[] AgentLegsEndedWithTheCall(CallSession session)
        => session.Legs
            .Where(leg => leg is not null &&
                leg.Role == CallPartyRole.Agent &&
                leg.AnsweredUtc.HasValue &&
                !leg.HangupCause.HasValue &&
                !string.IsNullOrWhiteSpace(leg.ProviderLegId) &&
                !string.Equals(leg.ProviderLegId, session.ProviderCallId, StringComparison.Ordinal))
            .Select(leg => leg.ProviderLegId)
            .ToArray();
}
