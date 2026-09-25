using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc />
public sealed class TransferredCallRouter : ITransferredCallRouter
{
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityReservationService _reservationService;
    private readonly IActivityQueueService _queueService;
    private readonly IVoiceQueueOfferService _offerService;
    private readonly IQueueTreatmentService _treatmentService;
    private readonly ITransferAgentReleaseService _agentRelease;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferredCallRouter"/> class.
    /// </summary>
    public TransferredCallRouter(
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        IAgentAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IActivityReservationManager reservationManager,
        IActivityReservationService reservationService,
        IActivityQueueService queueService,
        IVoiceQueueOfferService offerService,
        IQueueTreatmentService treatmentService,
        ITransferAgentReleaseService agentRelease,
        IContactCenterEventPublisher publisher,
        ISession session,
        IClock clock,
        ILogger<TransferredCallRouter> logger)
    {
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _availabilityService = availabilityService;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _reservationManager = reservationManager;
        _reservationService = reservationService;
        _queueService = queueService;
        _offerService = offerService;
        _treatmentService = treatmentService;
        _agentRelease = agentRelease;
        _publisher = publisher;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TransferResult> RouteToAgentAsync(TransferRoutingContext context, CancellationToken cancellationToken = default)
    {
        Validate(context);

        var target = await _agentManager.FindByIdAsync(context.TargetId, cancellationToken);

        if (target is null)
        {
            return TransferResult.Failure("The agent could not be found.");
        }

        if (string.Equals(target.ItemId, context.TransferringAgentId, StringComparison.Ordinal))
        {
            return TransferResult.Failure("A call cannot be transferred to the agent who is already on it.");
        }

        // Checked before anything changes, so an agent who cannot take the call leaves the caller where they are,
        // still talking to the agent who tried, rather than parked on hold for somebody who will never pick up.
        if ((await _availabilityService.GetForDirectAsync(target.ItemId, cancellationToken))?.Agent is null)
        {
            return TransferResult.Failure($"{DisplayName(target)} is not available to take the call.");
        }

        var interaction = context.Interaction;
        var originalQueueId = interaction.QueueId;
        var now = _clock.UtcNow;

        InteractionTransferHistory.Open(interaction, context.TransferringAgentId, InteractionTransferTargetType.Agent, target.ItemId, now, InteractionTransferHistory.OfferedToAgent);

        // The offer to one named agent is a direct offer, and a direct offer that is not answered goes to that agent's
        // voicemail after the standard ring window, whatever window the original entry point used.
        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = target.ItemId;
        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey] = ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

        var releasedLegs = await ReleaseTransferringAgentAsync(context, now, cancellationToken);
        var queue = string.IsNullOrEmpty(originalQueueId) ? null : await _queueManager.FindByIdAsync(originalQueueId, cancellationToken);

        await RequeueAsync(interaction.ActivityItemId, ContactCenterConstants.DirectRouting.QueueId, priority: InteractionPriority.High, cancellationToken);

        var offeredUserId = await _offerService.OfferToAgentAsync(
            interaction.ActivityItemId,
            ContactCenterConstants.DirectRouting.QueueId,
            target.ItemId,
            ringTimeoutSeconds: null,
            cancellationToken);

        // The offer carried the call under the direct-routing queue, which has no after-call work and no reports of
        // its own. The call still belongs to the queue it came in on, for the agent who takes it and for reporting.
        await RestoreQueueAsync(interaction.ItemId, originalQueueId, cancellationToken);
        await StartHoldMusicAsync(queue, interaction.ProviderInteractionId, cancellationToken);
        await _publisher.PublishAsync(
            TransferEventFactory.Transferred(interaction, context.TransferringAgentId, context.TransferringUserId, InteractionTransferType.Blind, InteractionTransferTargetType.Agent, target.ItemId, InteractionTransferHistory.OfferedToAgent, now),
            cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await _agentRelease.HangUpAsync(interaction.ProviderName, releasedLegs, cancellationToken);

        return TransferResult.Success(string.IsNullOrEmpty(offeredUserId)
            ? $"The call is holding for {DisplayName(target)}."
            : $"The call is ringing for {DisplayName(target)}.");
    }

    /// <inheritdoc />
    public async Task<TransferResult> RouteToQueueAsync(TransferRoutingContext context, CancellationToken cancellationToken = default)
    {
        Validate(context);

        var queue = await _queueManager.FindByIdAsync(context.TargetId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            return TransferResult.Failure("The queue is not available.");
        }

        var interaction = context.Interaction;
        var now = _clock.UtcNow;

        InteractionTransferHistory.Open(interaction, context.TransferringAgentId, InteractionTransferTargetType.Queue, queue.ItemId, now, InteractionTransferHistory.WaitingInQueue);

        var releasedLegs = await ReleaseTransferringAgentAsync(context, now, cancellationToken);

        // A caller who has already been answered and passed along is not new to the queue: they keep the priority
        // they had, and are never ranked below the queue's own default.
        var priority = await ResolvePriorityAsync(interaction.ActivityItemId, queue, cancellationToken);

        await RequeueAsync(interaction.ActivityItemId, queue.ItemId, priority, cancellationToken);
        await SetQueueAsync(interaction.ItemId, queue.ItemId, cancellationToken);

        var offeredUserId = await _offerService.OfferNextAsync(queue.ItemId, cancellationToken);

        // Nobody free: the caller is waiting, so the queue's treatment starts now rather than at the next sweep. An
        // offered caller is no longer a waiting one and would get nothing from that pass, so they get the music.
        if (string.IsNullOrEmpty(offeredUserId))
        {
            await RunTreatmentAsync(queue, cancellationToken);
        }
        else
        {
            await StartHoldMusicAsync(queue, interaction.ProviderInteractionId, cancellationToken);
        }

        await _publisher.PublishAsync(
            TransferEventFactory.Transferred(interaction, context.TransferringAgentId, context.TransferringUserId, InteractionTransferType.Blind, InteractionTransferTargetType.Queue, queue.ItemId, InteractionTransferHistory.WaitingInQueue, now),
            cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await _agentRelease.HangUpAsync(interaction.ProviderName, releasedLegs, cancellationToken);

        return TransferResult.Success(string.IsNullOrEmpty(offeredUserId)
            ? $"The call is waiting in {queue.Name}."
            : $"The call is ringing for the next agent in {queue.Name}.");
    }

    private async Task<IReadOnlyList<string>> ReleaseTransferringAgentAsync(TransferRoutingContext context, DateTime now, CancellationToken cancellationToken)
    {
        var releasedLegs = await _agentRelease.ReleaseAsync(context.Interaction, context.Session, context.TransferringAgentId, now, cancellationToken);

        // Committed before the agent's leg is hung up or anything re-routes the call. The hangup comes back as a
        // webhook, and one that finds the leg still carrying the call ends the call and hangs up the caller.
        await _session.SaveChangesAsync(cancellationToken);

        return releasedLegs;
    }

    // The accepted call is still Assigned in its queue, and enqueueing work that is Assigned returns the item it
    // already has, so the caller would never be offered anywhere. The accepted reservation also still claims the
    // activity, and a second offer for it is refused by the store. Both are finished first. The transferring agent
    // is already in wrap-up or ready by now, so undoing their reservation does not touch their state.
    private async Task RequeueAsync(string activityItemId, string queueId, InteractionPriority? priority, CancellationToken cancellationToken)
    {
        foreach (var reservation in await _reservationManager.GetActiveByActivityAsync(activityItemId, cancellationToken))
        {
            await _reservationService.CompensateAsync(reservation.ItemId, removeFromQueue: true, cancellationToken);
        }

        var existing = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        if (existing is not null && existing.Status is QueueItemStatus.Waiting or QueueItemStatus.Reserved or QueueItemStatus.Assigned)
        {
            await _queueService.DequeueAsync(existing, QueueItemStatus.Completed, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        await _queueService.EnqueueAsync(activityItemId, queueId, priority, cancellationToken);
    }

    private async Task<InteractionPriority?> ResolvePriorityAsync(string activityItemId, ActivityQueue queue, CancellationToken cancellationToken)
    {
        var existing = await _queueItemManager.FindByActivityIdAsync(activityItemId, cancellationToken);

        return existing is not null && existing.Priority > queue.DefaultPriority
            ? existing.Priority
            : queue.DefaultPriority;
    }

    // Enqueueing and offering commit, and a commit forgets what the session had loaded, so the interaction is read
    // again rather than saved from the copy taken before.
    private async Task RestoreQueueAsync(string interactionId, string queueId, CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || string.Equals(interaction.QueueId, queueId, StringComparison.Ordinal))
        {
            return;
        }

        interaction.QueueId = queueId;
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
    }

    private Task SetQueueAsync(string interactionId, string queueId, CancellationToken cancellationToken)
        => RestoreQueueAsync(interactionId, queueId, cancellationToken);

    private async Task RunTreatmentAsync(ActivityQueue queue, CancellationToken cancellationToken)
    {
        try
        {
            // The treatment pass finds who is waiting by querying the store, so the new queue item is committed first.
            await _session.SaveChangesAsync(cancellationToken);
            await _treatmentService.RunDueAsync(queue, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The caller is in the queue either way; losing the music must not lose the call.
            _logger.LogWarning(ex, "Could not start queue treatment for a caller transferred to queue '{QueueId}'.", queue.ItemId.SanitizeLogValue());
        }
    }

    private async Task StartHoldMusicAsync(ActivityQueue queue, string providerCallId, CancellationToken cancellationToken)
    {
        if (queue is null || string.IsNullOrWhiteSpace(providerCallId))
        {
            return;
        }

        try
        {
            await _treatmentService.StartHoldMusicAsync(queue, providerCallId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start hold music for a transferred caller from queue '{QueueId}'.", queue.ItemId.SanitizeLogValue());
        }
    }

    private static string DisplayName(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName)
            ? agent.DisplayName
            : !string.IsNullOrWhiteSpace(agent.UserName) ? agent.UserName : agent.Name;

    private static void Validate(TransferRoutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Interaction);
        ArgumentException.ThrowIfNullOrEmpty(context.TransferringAgentId);
        ArgumentException.ThrowIfNullOrEmpty(context.TargetId);
    }
}
