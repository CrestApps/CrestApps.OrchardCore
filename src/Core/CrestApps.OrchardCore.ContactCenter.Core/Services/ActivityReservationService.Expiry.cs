using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What becomes of an offer nobody answered: the sweep that finds reservations past their deadline, and the
/// release that decides where each of those callers goes next — back into the queue, to the agent's voicemail,
/// or out of the queue entirely — and puts the agent back to work. The same release moves on the caller whose
/// agent accepted but could not be reached.
/// </summary>
/// <remarks>
/// Split from the reservation lifecycle it shares a class with. Reserving, accepting and rejecting are things a
/// person does within seconds; this is a timer sweeping rows a person did not act on, and the two only have the
/// same dependencies in common.
/// </remarks>
public sealed partial class ActivityReservationService
{
    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
        => await ExpireDueCoreAsync(maxReservations: null, lockWait: _coordinationOptions.ReservationLockTimeout, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> ReclaimDueAsync(int maxReservations, CancellationToken cancellationToken = default)
    {
        if (maxReservations <= 0)
        {
            return 0;
        }

        return await ExpireDueCoreAsync(maxReservations, lockWait: _coordinationOptions.ReclaimLockWait, cancellationToken);
    }

    private static readonly TimeSpan _retryAfterContention = TimeSpan.FromSeconds(1);

    private async Task<int> ExpireDueCoreAsync(int? maxReservations, TimeSpan lockWait, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var count = 0;
        var examined = 0;
        DateTime? afterExpiresUtc = null;
        var afterDocumentId = 0L;

        // Drain the expiry backlog in bounded, oldest-first pages so a spike that leaves thousands of
        // reservations expired at once is processed in fixed-size batches instead of being materialized in a
        // single unbounded query. Paging is keyset (seek) based over the stable (ExpiresUtc, DocumentId)
        // order: each page advances the cursor past the last row it observed, regardless of whether that row
        // was expired here or is currently locked by another node. Because the cursor is an absolute position
        // rather than a numeric offset, concurrent expirations or insertions elsewhere in the backlog never
        // shift the window, so a live reservation is never skipped and a block of locked candidates at the
        // front never starves the drainable ones behind them. Candidates that could not be processed this run
        // (locked, or already changed) are simply retried on the next scheduled sweep, which restarts from the
        // oldest expired reservation. The loop stops when a page is short (the backlog is exhausted), when the
        // caller-supplied reservation budget is reached, or when the run is cancelled. Callers on a
        // latency-sensitive path pass a bounded budget and a short lock wait, so the pass is strictly bounded
        // and does not block on a reservation another node is already transitioning.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageSize = maxReservations is int max
                ? Math.Min(_coordinationOptions.ExpiryPageSize, max - examined)
                : _coordinationOptions.ExpiryPageSize;

            if (pageSize <= 0)
            {
                break;
            }

            var page = await _reservationManager.GetExpiredAsync(now, afterExpiresUtc, afterDocumentId, pageSize, cancellationToken);

            foreach (var candidate in page.Reservations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;

                if ((await TryExpireAsync(candidate.ItemId, now, lockWait, cancellationToken)).Outcome == DeadlineOutcome.Expired)
                {
                    count++;
                }
            }

            if (!page.HasMore)
            {
                break;
            }

            if (maxReservations is int budget && examined >= budget)
            {
                break;
            }

            afterExpiresUtc = page.NextAfterExpiresUtc;
            afterDocumentId = page.NextAfterDocumentId;
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task<DateTime?> ExpireAtDeadlineAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        var now = _clock.UtcNow;

        try
        {
            (var outcome, var expiresUtc) = await TryExpireAsync(reservationId, now, _coordinationOptions.ReservationLockTimeout, cancellationToken);

            return outcome switch
            {
                // Extended since the deadline was armed, or the timer ran early against the tenant clock.
                DeadlineOutcome.NotDue => expiresUtc,

                // Another transition held the offer for the whole wait; whichever way it went, a second look settles it.
                DeadlineOutcome.Locked => now.Add(_retryAfterContention),

                _ => null,
            };
        }
        catch (ConcurrencyException)
        {
            // An accept or a decline won the compare-and-set. A fresh scope reads what it did.
            return now.Add(_retryAfterContention);
        }
    }

    /// <summary>
    /// Expires one reservation when it is still ringing and due, under its reservation lock: the one place the sweep
    /// and the offer's own deadline timer both settle an unanswered offer, so they cannot disagree about when.
    /// </summary>
    private async Task<(DeadlineOutcome Outcome, DateTime ExpiresUtc)> TryExpireAsync(
        string reservationId,
        DateTime now,
        TimeSpan lockWait,
        CancellationToken cancellationToken)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            lockWait,
            _coordinationOptions.ReservationLockExpiration);

        if (!locked)
        {
            return (DeadlineOutcome.Locked, default);
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Pending)
        {
            return (DeadlineOutcome.Settled, default);
        }

        if (reservation.ExpiresUtc > now)
        {
            return (DeadlineOutcome.NotDue, reservation.ExpiresUtc);
        }

        await ReleaseAsync(reservation, ReservationStatus.Expired, cancellationToken);
        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        return (DeadlineOutcome.Expired, reservation.ExpiresUtc);
    }

    /// <summary>
    /// Releases an offer the agent did not take, or could not be reached for, and decides where its caller goes next.
    /// </summary>
    /// <param name="reservation">The reservation being released.</param>
    /// <param name="status">The terminal status the reservation takes.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <param name="sendToVoicemail">Whether the agent chose voicemail for the caller, whatever the queue would do.</param>
    /// <param name="agentUnreachable">Whether the agent accepted the offer but the leg rung to their phone failed: the
    /// queue's unanswered-offer action applies as it does to an offer that rang out, and the agent is set Away rather
    /// than returned to work.</param>
    private async Task ReleaseAsync(
        ActivityReservation reservation,
        ReservationStatus status,
        CancellationToken cancellationToken,
        bool sendToVoicemail = false,
        bool agentUnreachable = false)
    {
        var now = _clock.UtcNow;
        var wasAccepted = reservation.Status == ReservationStatus.Accepted;
        reservation.TransitionTo(status);

        // This is the age settled reservations are purged by. Without it the row is never selected by retention.
        reservation.ModifiedUtc = now;

        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null &&
            !string.IsNullOrWhiteSpace(queueItem.ReservationId) &&
            !string.Equals(queueItem.ReservationId, reservation.ItemId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Skipped releasing expired reservation '{ReservationId}' for activity '{ActivityItemId}' because queue item '{QueueItemId}' is now owned by newer reservation '{CurrentReservationId}'.",
                reservation.ItemId.SanitizeLogValue(),
                reservation.ActivityItemId.SanitizeLogValue(),
                queueItem.ItemId.SanitizeLogValue(),
                queueItem.ReservationId.SanitizeLogValue());

            var obsoleteAgent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

            if (obsoleteAgent is not null &&
                string.Equals(obsoleteAgent.ActiveReservationId, reservation.ItemId, StringComparison.Ordinal))
            {
                await ReleaseAgentStateAsync(
                    obsoleteAgent,
                    reservation,
                    ResolveReleaseReason(status),
                    await FindInteractionIdAsync(reservation.ActivityItemId, cancellationToken),
                    now,
                    cancellationToken);

                await _agentManager.UpdateAsync(obsoleteAgent, cancellationToken: cancellationToken);
                await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, obsoleteAgent, ContactCenterActor.System, now, cancellationToken);
            }

            await RecordOfferSettledAsync(reservation, interaction: null, obsoleteAgent, now, CallLifecycleReasons.Superseded, cancellationToken);

            return;
        }

        var queue = !string.IsNullOrEmpty(reservation.QueueId)
            ? await _queueManager.FindByIdAsync(reservation.QueueId, cancellationToken)
            : null;
        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);
        // A direct-to-agent (personal line) offer has no queue to requeue into: when the named agent lets the
        // offer expire or declines it, the caller is sent to that agent's voicemail rather than stranded --
        // unless the entry point disabled voicemail (ring window 0), in which case the held call is requeued so
        // it can be re-offered when the agent is next available. A cancel (for example the caller hanging up
        // while it rings) keeps the shared release behavior. An agent who sends the offer to voicemail has chosen
        // for the caller, whatever the queue or line would have done.
        var isDirect = ContactCenterConstants.IsDirectRoutingQueue(reservation.QueueId);
        var directVoicemailEnabled = isDirect && IsDirectVoicemailEnabled(interaction);
        var configuredUnansweredAction = sendToVoicemail
            ? UnansweredOfferAction.Voicemail
            : ResolveUnansweredAction(isDirect, directVoicemailEnabled, status, queue, agentUnreachable);
        var unansweredAction = configuredUnansweredAction;
        ProviderCommandRegistration providerCommand = null;

        if (unansweredAction is UnansweredOfferAction.Voicemail or UnansweredOfferAction.Reject)
        {
            if (interaction is null ||
                string.IsNullOrWhiteSpace(interaction.ProviderInteractionId) ||
                string.IsNullOrWhiteSpace(interaction.ProviderName) ||
                _providerCommandStateService is null)
            {
                _logger.LogWarning(
                    "The unanswered-offer action '{UnansweredOfferAction}' could not be persisted for activity '{ActivityItemId}' because provider command infrastructure or call identity is unavailable.",
                    unansweredAction,
                    interaction?.ActivityItemId.SanitizeLogValue());
                unansweredAction = UnansweredOfferAction.Requeue;
            }
            else
            {
                var commandId = IdGenerator.GenerateId();
                interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId] = commandId;
                providerCommand = new ProviderCommandRegistration
                {
                    CommandId = commandId,
                    ProviderName = interaction.ProviderName,
                    CommandType = unansweredAction == UnansweredOfferAction.Voicemail
                        ? ProviderCommandType.SendToVoicemail
                        : ProviderCommandType.Reject,
                    ActivityItemId = reservation.ActivityItemId,
                    InteractionId = interaction.ItemId,
                    RemoveReservationFromQueueOnFailure = false,
                    RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
                    {
                        Initiator = CallControlInitiator.System,
                        ActivityItemId = reservation.ActivityItemId,
                        InteractionId = interaction.ItemId,
                        QueueId = reservation.QueueId,
                        AgentId = reservation.AgentId,
                        AgentUserId = agent?.UserId,
                        ProviderCallId = interaction.ProviderInteractionId,
                        ReofferOnFailure = true,
                        Metadata = BuildOfferTimeoutMetadata(queue, agent),
                    }),
                };
            }
        }

        var requeue = unansweredAction == UnansweredOfferAction.Requeue;

        if (queueItem is not null)
        {
            queueItem.ReservationId = null;
            queueItem.AgentId = null;

            if (requeue)
            {
                queueItem.TransitionTo(QueueItemStatus.Waiting);

                // An agent who declined the caller, or let them ring out, is not offered them straight back.
                if (IsTurnedDownByTheAgent(status, reservation))
                {
                    queueItem.RecordDecline(reservation.AgentId, now);
                }

                await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
            }
            else
            {
                queueItem.DequeuedUtc = now;
                await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
            }
        }

        var agentPreviousStatus = agent?.PresenceStatus ?? AgentPresenceStatus.Offline;

        if (agent is not null)
        {
            if (agentUnreachable)
            {
                await SetUnreachableAgentAwayAsync(agent, reservation, interaction?.ItemId, now, cancellationToken);
            }
            else
            {
                await ReleaseAgentStateAsync(agent, reservation, ResolveReleaseReason(status), interaction?.ItemId, now, cancellationToken);
            }

            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
        }

        await _workStateService.MutateAsync(reservation.ActivityItemId, workState =>
        {
            workState.TransitionTo(requeue
                ? ActivityAssignmentStatus.Available
                : ActivityAssignmentStatus.Released);
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;

            // An accepted offer had become an assignment; the caller is nobody's again.
            if (wasAccepted)
            {
                workState.AssignedToId = null;
                workState.AssignedToUsername = null;
                workState.AssignedToUtc = null;
            }
        }, cancellationToken);

        if (!requeue)
        {
            var terminalStatus = unansweredAction == UnansweredOfferAction.Voicemail
                ? ActivityStatus.Completed
                : ActivityStatus.Cancelled;

            await _activityWriter.ScheduleUpdateAsync(reservation.ActivityItemId, activity =>
            {
                activity.Status = terminalStatus;
                activity.CompletedUtc = now;

                if (terminalStatus == ActivityStatus.Completed)
                {
                    ActivityDispositionActors.Stamp(activity, ActivityDispositionActor.System);
                }
            }, cancellationToken);
        }

        // Releasing an offer races the conversation ending. The customer can abandon while the offer is still
        // ringing an agent, the provider event settles the interaction, and this sweep then arrives to return
        // work that no longer exists to routing. Returning a settled interaction to routing is refused by the
        // lifecycle, and this path runs from a background sweep that releases every due reservation, so letting
        // that refusal escape would abandon the rest of the sweep over one call that had already hung up. The
        // reservation, queue item, agent and work state are still released; only the re-offer is skipped.
        var reofferNow = requeue &&
            interaction is not null &&
            !interaction.IsSettled &&
            IsWaitingForTheNextAgent(status, agentUnreachable, reservation.QueueId);

        if (interaction is not null && !interaction.IsSettled)
        {
            if (requeue)
            {
                interaction.Requeue();
            }
            else if (providerCommand is not null)
            {
                interaction.Reoffer();
                interaction.EndedUtc = null;
                interaction.AgentId = null;
                interaction.TechnicalMetadata["unansweredOfferAction"] = unansweredAction.ToString();
            }
            else
            {
                interaction.TransitionTo(InteractionStatus.Ended);
                interaction.EndedUtc ??= now;
                interaction.TechnicalMetadata["unansweredOfferAction"] = unansweredAction.ToString();
            }

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, agent, ContactCenterActor.System, now, cancellationToken);

        if (agentUnreachable && agent is not null)
        {
            await PublishPresenceChangedAsync(agent, agentPreviousStatus, now, cancellationToken);
        }

        // The caller is back in the queue for the next agent in line, who is offered them now rather than at the
        // minute's assignment pass: an unanswered caller is often gone by then. This is published here, where the
        // action actually taken is known, so a queue set to voicemail or reject that fell back to requeueing is
        // re-offered too. It follows the release, so the leg rung for this offer is hung up before the next rings.
        if (reofferNow)
        {
            await PublishAsync(ContactCenterConstants.Events.OfferRequeued, reservation, agent, ContactCenterActor.System, now, cancellationToken);
        }

        // A decline is recorded by whoever took it, with the agent as the actor; an expiry names what became of the
        // caller, and a withdrawal says the platform took the offer back. An accepted offer was already settled when
        // the agent took it, so releasing it records nothing more.
        if (!wasAccepted)
        {
            await RecordOfferSettledAsync(
                reservation,
                interaction,
                agent,
                now,
                status == ReservationStatus.Expired ? configuredUnansweredAction.ToString() : CallLifecycleReasons.Withdrawn,
                cancellationToken);
        }

        if (providerCommand is not null)
        {
            await _providerCommandStateService.RegisterAsync(providerCommand, cancellationToken);
            _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
                processor.DispatchAsync(providerCommand.CommandId, CancellationToken.None));
        }
    }

    private async Task CommitTransitionAsync(
        string activityItemId,
        string agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "A concurrent Contact Center operation won the compare-and-set transition for activity '{ActivityId}' and agent '{AgentId}'.",
                    activityItemId.SanitizeLogValue(),
                    agentId.SanitizeLogValue());
            }

            throw;
        }
    }

    private enum DeadlineOutcome
    {
        Expired,
        Locked,
        NotDue,
        Settled,
    }

    /// <summary>
    /// Resolves what becomes of the caller. The queue's unanswered-offer action applies to an offer that rang out,
    /// and to one the agent accepted but could not be reached for, which left the caller waiting the same way.
    /// </summary>
    private static UnansweredOfferAction ResolveUnansweredAction(
        bool isDirect,
        bool directVoicemailEnabled,
        ReservationStatus status,
        ActivityQueue queue,
        bool agentUnreachable)
        => isDirect
            ? directVoicemailEnabled && status is ReservationStatus.Expired or ReservationStatus.Rejected
                ? UnansweredOfferAction.Voicemail
                : UnansweredOfferAction.Requeue
            : status == ReservationStatus.Expired || agentUnreachable
                ? queue?.UnansweredOfferAction ?? UnansweredOfferAction.Requeue
                : UnansweredOfferAction.Requeue;

    /// <summary>
    /// Whether the released offer was turned down by the agent it rang -- declined, or left to ring out -- in a queue
    /// with other agents to offer it to next.
    /// </summary>
    /// <remarks>
    /// A caller who hangs up, or an offer the platform withdraws, is cancelled rather than rejected, and says nothing
    /// about the agent. A direct line rings only the agent it belongs to, and a campaign's virtual queue carries
    /// outbound inventory the dialer paces, so neither has a next agent in line.
    /// </remarks>
    private static bool IsTurnedDownByTheAgent(ReservationStatus status, ActivityReservation reservation)
        => status is ReservationStatus.Rejected or ReservationStatus.Expired &&
            !string.IsNullOrEmpty(reservation.AgentId) &&
            !ContactCenterConstants.IsDirectRoutingQueue(reservation.QueueId) &&
            !ContactCenterConstants.IsCampaignQueue(reservation.QueueId);

    /// <summary>
    /// Whether a caller put back into the queue is waiting for the next agent in line: after an offer that rang out,
    /// or one the agent accepted but could not be reached for.
    /// </summary>
    /// <remarks>
    /// A decline re-offers the caller itself, and a cancel (the caller hanging up, the platform withdrawing the offer)
    /// leaves nobody waiting. A direct line rings only the agent it belongs to, and a campaign's virtual queue is
    /// paced by the dialer, so neither has a next agent in line.
    /// </remarks>
    private static bool IsWaitingForTheNextAgent(ReservationStatus status, bool agentUnreachable, string queueId)
        => (status == ReservationStatus.Expired || agentUnreachable) &&
            !string.IsNullOrEmpty(queueId) &&
            !ContactCenterConstants.IsDirectRoutingQueue(queueId) &&
            !ContactCenterConstants.IsCampaignQueue(queueId);

    private static bool IsDirectVoicemailEnabled(Interaction interaction)
    {
        // Voicemail is enabled for a direct-to-agent call unless the entry point explicitly set its ring window
        // to 0. Absent metadata (older data) defaults to enabled.
        if (interaction is not null &&
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey, out var value) &&
            value is not null &&
            int.TryParse(value.ToString(), out var seconds))
        {
            return seconds > 0;
        }

        return true;
    }

    private static Dictionary<string, object> BuildOfferTimeoutMetadata(ActivityQueue queue, AgentProfile agent)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (queue is not null)
        {
            metadata["queueId"] = queue.ItemId;

            if (!string.IsNullOrWhiteSpace(queue.Name))
            {
                metadata["queueName"] = queue.Name;
            }
        }

        if (agent is not null)
        {
            if (!string.IsNullOrWhiteSpace(agent.UserId))
            {
                metadata["voicemailRecipientUserId"] = agent.UserId;
            }

            if (!string.IsNullOrWhiteSpace(agent.UserName))
            {
                metadata["voicemailRecipientUserName"] = agent.UserName;
            }

            if (!string.IsNullOrWhiteSpace(agent.DisplayName))
            {
                metadata["voicemailRecipientDisplayName"] = agent.DisplayName;
            }
        }

        return metadata;
    }
}
