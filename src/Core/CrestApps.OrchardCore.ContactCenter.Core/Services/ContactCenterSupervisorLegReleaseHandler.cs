using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Hangs up the supervisor legs a call still had when it ended, so a supervisor is not left on a line the call has left.
/// </summary>
/// <remarks>
/// A supervisor listening through a conference stays in it when the customer and the agent are gone: nothing on the
/// provider's side ends it for them. When a call ends every engagement that ended with it is handed to the provider to
/// release, and the supervisor's clients are told. Releasing a leg that is already gone is not an error, so a
/// redelivered event does nothing new.
/// </remarks>
public sealed class ContactCenterSupervisorLegReleaseHandler : IContactCenterEventHandler
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ISupervisorEngagementNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSupervisorLegReleaseHandler"/> class.
    /// </summary>
    public ContactCenterSupervisorLegReleaseHandler(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<ISupervisorEngagementNotifier> notifiers,
        IClock clock,
        ILogger<ContactCenterSupervisorLegReleaseHandler> logger)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _notifier = notifiers?.FirstOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/SupervisorLegRelease/v1";

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
            _voiceProviderResolver.Get(session.ProviderName) is not IContactCenterVoiceSupervisorInterventionProvider provider)
        {
            return;
        }

        foreach (var engagement in EngagementsEndedWithTheCall(session))
        {
            await provider.ReleaseSupervisorLegAsync(engagement.ProviderLegId, cancellationToken);

            if (_notifier is not null)
            {
                await _notifier.NotifyEngagementAsync(new SupervisorEngagementNotification
                {
                    State = SupervisorEngagementNotification.Ended,
                    InteractionId = interactionId,
                    SupervisorUserId = engagement.SupervisorUserId,
                    AgentId = session.AgentId,
                    Mode = engagement.Mode.ToString(),
                    Reason = "call-ended",
                    ServerTimeUtc = _clock.UtcNow,
                }, CancellationToken.None);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Released supervisor leg '{SupervisorLegId}' of ended call session '{CallSessionId}'.",
                    engagement.ProviderLegId.SanitizeLogValue(),
                    session.ItemId.SanitizeLogValue());
            }
        }
    }

    // How far before the call's recorded end an engagement's end may be stamped and still be the call's end: the two are
    // stamped by separate steps of the same delivery.
    private static readonly TimeSpan _endedWithTheCallTolerance = TimeSpan.FromSeconds(5);

    // An engagement the call's end closed carries the call's end time; one that ended earlier was released then. A leg
    // the supervisor took the call over on is an agent leg now, and is released with the agent legs.
    private static MonitorSession[] EngagementsEndedWithTheCall(CallSession session)
        => session.MonitorSessions
            .Where(engagement =>
                engagement is not null &&
                !string.IsNullOrWhiteSpace(engagement.ProviderLegId) &&
                (!engagement.EndedUtc.HasValue ||
                    !session.EndedUtc.HasValue ||
                    engagement.EndedUtc.Value >= session.EndedUtc.Value - _endedWithTheCallTolerance) &&
                !session.Legs.Any(leg => leg is not null && leg.Role == CallPartyRole.Agent && string.Equals(leg.ProviderLegId, engagement.ProviderLegId, StringComparison.Ordinal)))
            .ToArray();
}
