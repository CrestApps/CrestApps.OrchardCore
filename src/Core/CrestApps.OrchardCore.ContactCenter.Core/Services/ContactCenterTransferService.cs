using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterTransferService"/>.
/// </summary>
public sealed class ContactCenterTransferService : IContactCenterTransferService
{
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueService _queueService;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="queueService">The queue service used to re-enqueue queue transfers.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp transfer times.</param>
    public ContactCenterTransferService(
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _interactionManager = interactionManager;
        _queueService = queueService;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TransferResult.Failure("A transfer requires an interaction.");
        }

        if (string.IsNullOrEmpty(request.TargetId))
        {
            return TransferResult.Failure("A transfer requires a destination.");
        }

        var interaction = await _interactionManager.FindByIdAsync(request.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return TransferResult.Failure("The interaction could not be found.");
        }

        var now = _clock.UtcNow;
        var entry = new InteractionTransferHistoryEntry
        {
            FromParticipantId = request.InitiatedByAgentId ?? interaction.AgentId,
            ToParticipantId = request.TargetId,
            TargetType = request.TargetType.ToString(),
            RequestedUtc = now,
        };

        var reason = await ApplyTargetAsync(request, interaction, cancellationToken);

        entry.CompletedUtc = now;
        entry.Result = reason;
        interaction.TransferHistory.Add(entry);
        interaction.Status = InteractionStatus.Transferring;

        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.InteractionTransferred,
            InteractionId = interaction.ItemId,
            AggregateType = nameof(Interaction),
            AggregateId = interaction.ItemId,
            ActorId = request.InitiatedByAgentId ?? interaction.AgentId,
            SourceComponent = ContactCenterConstants.Components.Interactions,
        };

        await _publisher.PublishAsync(interactionEvent, cancellationToken);

        return TransferResult.Success(reason);
    }

    private async Task<string> ApplyTargetAsync(TransferRequest request, Interaction interaction, CancellationToken cancellationToken)
    {
        switch (request.TargetType)
        {
            case InteractionTransferTargetType.Queue:
                if (!string.IsNullOrEmpty(interaction.ActivityItemId))
                {
                    await _queueService.EnqueueAsync(interaction.ActivityItemId, request.TargetId, priority: null, cancellationToken);

                    return "Re-queued to the target queue.";
                }

                return "Queued transfer requested without an activity.";
            case InteractionTransferTargetType.Agent:
                return "Transfer to agent requested.";
            case InteractionTransferTargetType.External:
                return "Transfer to external destination requested.";
            case InteractionTransferTargetType.EntryPoint:
                return "Transfer to entry point requested.";
            default:
                return "Transfer requested.";
        }
    }
}
