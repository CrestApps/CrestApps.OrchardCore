using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Closes out a transfer the caller did not wait for.
/// </summary>
/// <remarks>
/// A caller who hangs up while held for a consult leaves the consulting agent and the destination talking to each
/// other about somebody who is gone, and the destination's leg is one the platform placed, so nothing else hangs it
/// up. A caller who hangs up while being offered to the next agent never reached anybody, which the transfer history
/// has to say rather than leaving the transfer looking as if it were still under way.
/// </remarks>
public sealed class ContactCenterTransferCallEndedHandler : IContactCenterEventHandler
{
    private readonly ICallSessionManager _callSessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly Lazy<IConsultTransferService> _consults;
    private readonly Lazy<IAgentPresenceManager> _presenceManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferCallEndedHandler"/> class.
    /// </summary>
    public ContactCenterTransferCallEndedHandler(
        ICallSessionManager callSessionManager,
        IInteractionManager interactionManager,
        Lazy<IConsultTransferService> consults,
        Lazy<IAgentPresenceManager> presenceManager,
        IClock clock)
    {
        _callSessionManager = callSessionManager;
        _interactionManager = interactionManager;
        _consults = consults;
        _presenceManager = presenceManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/TransferCallEnded/v1";

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

        if (session is not null && CallSessionLifecycle.IsTerminal(session.State))
        {
            var connectedAgentConsults = session.Consults
                .Where(consult =>
                    consult is not null &&
                    consult.Status == ConsultCallStatus.Connected &&
                    consult.TargetType == InteractionTransferTargetType.Agent &&
                    !string.IsNullOrEmpty(consult.TargetId))
                .Select(consult => consult.TargetId)
                .ToArray();

            if (await _consults.Value.EndForCallerHangupAsync(session.ItemId, cancellationToken) > 0)
            {
                // Made busy by answering the consult; with the caller gone there is nothing left to hand over.
                foreach (var agentId in connectedAgentConsults)
                {
                    await _presenceManager.Value.CompleteWorkAsync(agentId, new AgentStateChangeContext
                    {
                        InteractionId = interactionId,
                        ChangedUtc = _clock.UtcNow,
                    }, cancellationToken);
                }
            }
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is not null &&
            interaction.IsSettled &&
            InteractionTransferHistory.AbandonPending(interaction, InteractionTransferHistory.CallerHungUp))
        {
            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }
    }
}
