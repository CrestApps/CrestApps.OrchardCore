using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IStaleAgentWorkService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Work is stale only when something shows it is: the provider says the call is gone (or, for a soft-phone call, cannot
/// resolve it for long enough), the browser that placed the call has gone quiet, or a state has nothing behind it past the
/// grace period. Anything else -- above all a call the provider confirms is alive -- counts as live work, and while an
/// agent has any, nothing of theirs is released.
/// </para>
/// </remarks>
public sealed class StaleAgentWorkService : IStaleAgentWorkService
{
    private const string TelephonyCallReason = "A phone call is still open although the provider no longer has it.";
    private const string UnresolvedCallReason = "A phone call is still open although the provider cannot resolve it.";
    private const string AbandonedCallReason = "A phone call is still open although the browser that placed it is gone.";
    private const string ProviderInteractionReason = "A call is still open although the provider reports it ended.";
    private const string PresenceReason = "The agent is marked busy with no call in progress.";
    private const string ReservationReason = "The agent still holds a reservation that no offer or call backs.";
    private const string QueueItemReason = "Work is still assigned to the agent although no call is in progress.";

    private readonly IAgentSessionManager _sessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IAgentWorkStateHealingService _healingService;
    private readonly IProviderCallStateSynchronizationService _synchronizationService;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IActivityReservationManager _reservationManager;
    private readonly ITelephonyInteractionStore _telephonyInteractions;
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly IDistributedCache _cache;
    private readonly string _tenantName;
    private readonly AgentAvailabilityOptions _availabilityOptions;
    private readonly StaleAgentWorkOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaleAgentWorkService"/> class.
    /// </summary>
    /// <param name="sessionManager">The agent session manager, which says whether an agent's workspace is live.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="healingService">The service that releases an agent's reservation.</param>
    /// <param name="synchronizationServices">The provider call-state synchronization service.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="reservationManagers">The reservation manager, registered only with queues and campaigns.</param>
    /// <param name="telephonyInteractions">The soft-phone call store, registered only with telephony.</param>
    /// <param name="providerResolvers">The telephony provider resolver, registered only with telephony.</param>
    /// <param name="cache">The distributed cache provider answers are reused from.</param>
    /// <param name="shellSettings">The current tenant, which keys the cached answers so tenants never see each other's.</param>
    /// <param name="availabilityOptions">The availability policy, whose grace period applies to presence and routing state.</param>
    /// <param name="options">The stale-work policy.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public StaleAgentWorkService(
        IAgentSessionManager sessionManager,
        IInteractionManager interactionManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IAgentWorkStateHealingService healingService,
        IEnumerable<IProviderCallStateSynchronizationService> synchronizationServices,
        IContactCenterWorkStateService workStateService,
        IEnumerable<IActivityReservationManager> reservationManagers,
        IEnumerable<ITelephonyInteractionStore> telephonyInteractions,
        IEnumerable<ITelephonyProviderResolver> providerResolvers,
        IDistributedCache cache,
        ShellSettings shellSettings,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        IOptions<StaleAgentWorkOptions> options,
        IClock clock,
        ILogger<StaleAgentWorkService> logger)
    {
        _sessionManager = sessionManager;
        _interactionManager = interactionManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _healingService = healingService;
        _synchronizationService = synchronizationServices?.FirstOrDefault();
        _workStateService = workStateService;
        _reservationManager = reservationManagers?.FirstOrDefault();
        _telephonyInteractions = telephonyInteractions?.FirstOrDefault();
        _providerResolver = providerResolvers?.FirstOrDefault();
        _cache = cache;
        _tenantName = shellSettings.Name;
        _availabilityOptions = availabilityOptions.Value;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, StaleAgentWork>> DetectAsync(
        IReadOnlyCollection<AgentProfile> agents,
        CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<string, StaleAgentWork>(StringComparer.Ordinal);
        var candidates = (agents ?? [])
            .Where(agent => agent is not null && !string.IsNullOrEmpty(agent.ItemId))
            .ToArray();

        if (candidates.Length == 0)
        {
            return found;
        }

        var snapshot = await LoadAsync(candidates, cancellationToken);

        foreach (var agent in candidates)
        {
            var work = await EvaluateAsync(agent, snapshot, useCachedAnswers: true, cancellationToken);

            if (work.IsStale)
            {
                found[agent.ItemId] = work;
            }
        }

        return found;
    }

    /// <inheritdoc/>
    public async Task<StaleAgentWorkRelease> ReleaseAsync(
        AgentProfile agent,
        ContactCenterActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var snapshot = await LoadAsync([agent], cancellationToken);
        var work = await EvaluateAsync(agent, snapshot, useCachedAnswers: false, cancellationToken);

        if (work.HasLiveWork)
        {
            return new StaleAgentWorkRelease
            {
                DeclinedForLiveCall = true,
                Reason = work.ProviderConfirmsLiveCall
                    ? "The provider confirms a call is still in progress for this agent, so nothing was released."
                    : "This agent still has a call in progress that cannot be shown to be over, so nothing was released.",
            };
        }

        if (!work.IsStale)
        {
            return new StaleAgentWorkRelease { Reason = "This agent has nothing stale to release." };
        }

        var now = _clock.UtcNow;
        var release = new StaleAgentWorkRelease();

        foreach (var item in work.Items.Where(item => item.Kind == StaleAgentWorkKind.TelephonyCall))
        {
            if (await SettleTelephonyCallAsync(item, now, cancellationToken))
            {
                release.ReleasedItems.Add(item);
            }
        }

        foreach (var item in work.Items.Where(item => item.Kind == StaleAgentWorkKind.ProviderInteraction))
        {
            var interaction = await _interactionManager.FindByIdAsync(item.ReferenceId, cancellationToken);

            if (interaction is not null && _synchronizationService is not null)
            {
                interaction = await _synchronizationService.RefreshInteractionAsync(interaction, cancellationToken);
            }

            if (interaction is null || interaction.IsSettled)
            {
                release.ReleasedItems.Add(item);
            }
        }

        // An interaction the provider's truth could not settle keeps the agent on a call, and releasing the rest would
        // route new work to an agent the platform still believes is on one.
        if (release.ReleasedItems.Count(item => item.Kind is StaleAgentWorkKind.TelephonyCall or StaleAgentWorkKind.ProviderInteraction) <
            work.Items.Count(item => item.Kind is StaleAgentWorkKind.TelephonyCall or StaleAgentWorkKind.ProviderInteraction))
        {
            release.Released = release.ReleasedItems.Count > 0;
            release.Reason = "Some of the agent's calls could not be settled, so the rest of their work was left as it is.";

            return release;
        }

        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;

        await _healingService.HealForResetAsync(agent.ItemId, cancellationToken);

        foreach (var item in work.Items.Where(item => item.Kind == StaleAgentWorkKind.QueueItem))
        {
            if (await ReleaseQueueItemAsync(item, agent, cancellationToken))
            {
                release.ReleasedItems.Add(item);
            }
        }

        foreach (var item in work.Items.Where(item => item.Kind is StaleAgentWorkKind.Reservation or StaleAgentWorkKind.Presence))
        {
            release.ReleasedItems.Add(item);
        }

        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;
        release.ReturnedTo = await ReturnToReadyAsync(agent, actor, now, cancellationToken);
        release.Released = true;
        release.Reason = release.ReturnedTo.HasValue
            ? $"Released {release.ReleasedItems.Count} stale item(s) and returned the agent to {release.ReturnedTo}."
            : $"Released {release.ReleasedItems.Count} stale item(s).";

        _logger.LogWarning(
            "Released {Count} stale work item(s) held by Contact Center agent '{AgentId}'.",
            release.ReleasedItems.Count,
            agent.ItemId.SanitizeLogValue());

        return release;
    }

    private async Task<AgentPresenceStatus?> ReturnToReadyAsync(
        AgentProfile agent,
        ContactCenterActor actor,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (agent.PresenceStatus is not (AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp) ||
            string.IsNullOrEmpty(agent.UserId))
        {
            return null;
        }

        var target = agent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(agent);
        var context = new AgentStateChangeContext
        {
            Actor = actor,
            Source = AgentStateChangeSources.StaleWorkReleased,
            ChangedUtc = now,
        };

        try
        {
            // The call a dialer held the agent for is among the work just released, so its hold goes with it; left on
            // file, it would defer the return to ready behind a call that is over.
            if (!string.IsNullOrEmpty(agent.DialHoldReservationId))
            {
                agent.DialHoldReservationId = null;
                await _agentManager.UpdateAsync(agent, cancellationToken: CancellationToken.None);
            }

            // A Busy or wrapping-up agent is moved on the way their own work ending would move them: setting a state on
            // an agent the platform believes is working is deferred until that work ends, which here it never would.
            var updated = agent.PresenceStatus == AgentPresenceStatus.Reserved
                ? await _presenceManager.SetPresenceAsync(agent.UserId, target, reason: null, context, CancellationToken.None)
                : await _presenceManager.CompleteWorkAsync(agent.ItemId, context, CancellationToken.None);

            return updated is not null && updated.PresenceStatus is not (AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp)
                ? updated.PresenceStatus
                : null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not return Contact Center agent '{AgentId}' to {State} after releasing their stale work.",
                agent.ItemId.SanitizeLogValue(),
                target);

            return null;
        }
    }

    private async Task<bool> SettleTelephonyCallAsync(StaleAgentWorkItem item, DateTime now, CancellationToken cancellationToken)
    {
        if (_telephonyInteractions is null || string.IsNullOrEmpty(item.ReferenceId))
        {
            return false;
        }

        var settled = await _telephonyInteractions.UpdateByIdAsync(
            item.ReferenceId,
            call =>
            {
                // Settled by something else while the supervisor was deciding: nothing left to do, and nothing to overwrite.
                if (call.Outcome != CallOutcome.InProgress || call.EndedUtc.HasValue)
                {
                    return false;
                }

                call.Outcome = CallOutcome.Failed;
                call.EndedUtc = now;
                call.DurationSeconds = call.StartedUtc == default ? 0 : Math.Max(0, (now - call.StartedUtc).TotalSeconds);
                call.AwaitingAnswer = false;

                return true;
            },
            cancellationToken);

        return settled is not null;
    }

    // A reserved or assigned item with no live work behind it goes back where it belongs: complete when its call is over,
    // waiting when it was never taken.
    private async Task<bool> ReleaseQueueItemAsync(StaleAgentWorkItem item, AgentProfile agent, CancellationToken cancellationToken)
    {
        var queueItem = await _queueItemManager.FindByIdAsync(item.ReferenceId, cancellationToken);

        if (queueItem is null ||
            queueItem.Status is not (QueueItemStatus.Reserved or QueueItemStatus.Assigned) ||
            !string.Equals(queueItem.AgentId, agent.ItemId, StringComparison.Ordinal))
        {
            return queueItem is null || queueItem.IsSettled || queueItem.Status == QueueItemStatus.Waiting;
        }

        var interaction = string.IsNullOrEmpty(queueItem.ActivityItemId)
            ? null
            : await _interactionManager.FindByActivityIdAsync(queueItem.ActivityItemId, cancellationToken);

        if (interaction is not null && interaction.IsSettled)
        {
            queueItem.TransitionTo(QueueItemStatus.Completed);
            queueItem.DequeuedUtc ??= _clock.UtcNow;
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

            return true;
        }

        queueItem.TransitionTo(QueueItemStatus.Waiting);
        queueItem.ReservationId = null;
        queueItem.AgentId = null;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(queueItem.ActivityItemId))
        {
            await _workStateService.MutateAsync(queueItem.ActivityItemId, workState =>
            {
                workState.TransitionTo(ActivityAssignmentStatus.Available);
                workState.AssignedToId = null;
                workState.AssignedToUsername = null;
                workState.AssignedToUtc = null;
                workState.ReservationId = null;
                workState.ReservedById = null;
                workState.ReservedByUsername = null;
                workState.ReservedUtc = null;
                workState.ReservationExpiresUtc = null;
            }, cancellationToken);
        }

        return true;
    }

    private async Task<Snapshot> LoadAsync(IReadOnlyCollection<AgentProfile> agents, CancellationToken cancellationToken)
    {
        var snapshot = new Snapshot();
        var userIds = agents
            .Where(agent => !string.IsNullOrEmpty(agent.UserId))
            .Select(agent => agent.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var agentIds = agents.Select(agent => agent.ItemId).Distinct(StringComparer.Ordinal).ToArray();

        if (userIds.Length > 0)
        {
            foreach (var session in await _sessionManager.GetByUserIdsAsync(userIds, cancellationToken) ?? [])
            {
                snapshot.Sessions[session.UserId] = session;
            }

            if (_telephonyInteractions is not null)
            {
                var wanted = new HashSet<string>(userIds, StringComparer.Ordinal);

                foreach (var call in await _telephonyInteractions.GetActiveAsync(ActiveCallScanLimit, cancellationToken) ?? [])
                {
                    if (call is null || call.EndedUtc.HasValue || call.Outcome != CallOutcome.InProgress || !wanted.Contains(call.UserId ?? string.Empty))
                    {
                        continue;
                    }

                    if (!snapshot.Calls.TryGetValue(call.UserId, out var calls))
                    {
                        snapshot.Calls[call.UserId] = calls = [];
                    }

                    calls.Add(call);
                }
            }
        }

        foreach (var interaction in await _interactionManager.GetActiveByAgentIdsAsync(agentIds, cancellationToken) ?? [])
        {
            if (string.IsNullOrEmpty(interaction.AgentId))
            {
                continue;
            }

            if (!snapshot.Interactions.TryGetValue(interaction.AgentId, out var interactions))
            {
                snapshot.Interactions[interaction.AgentId] = interactions = [];
            }

            interactions.Add(interaction);
        }

        foreach (var queueItem in await _queueItemManager.GetOpenByAgentIdsAsync(agentIds, cancellationToken) ?? [])
        {
            if (string.IsNullOrEmpty(queueItem.AgentId))
            {
                continue;
            }

            if (!snapshot.QueueItems.TryGetValue(queueItem.AgentId, out var queueItems))
            {
                snapshot.QueueItems[queueItem.AgentId] = queueItems = [];
            }

            queueItems.Add(queueItem);
        }

        return snapshot;
    }

    private async Task<StaleAgentWork> EvaluateAsync(
        AgentProfile agent,
        Snapshot snapshot,
        bool useCachedAnswers,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var work = new StaleAgentWork { AgentId = agent.ItemId };

        snapshot.Sessions.TryGetValue(agent.UserId ?? string.Empty, out var session);

        var sessionIsLive = session is not null &&
            session.IsOnline &&
            session.ConnectionIds.Count > 0 &&
            session.LastHeartbeatUtc.HasValue &&
            session.LastHeartbeatUtc.Value >= now - GetHeartbeatTimeout();

        if (snapshot.Calls.TryGetValue(agent.UserId ?? string.Empty, out var calls))
        {
            foreach (var call in calls)
            {
                var verdict = await JudgeCallAsync(call, sessionIsLive, now, useCachedAnswers, cancellationToken);

                if (verdict.Item is not null)
                {
                    work.Items.Add(verdict.Item);
                }
                else
                {
                    work.HasLiveWork = true;
                    work.ProviderConfirmsLiveCall |= verdict.ProviderConfirmsLive;
                }
            }
        }

        if (snapshot.Interactions.TryGetValue(agent.ItemId, out var interactions))
        {
            foreach (var interaction in interactions)
            {
                var verdict = await JudgeInteractionAsync(interaction, now, useCachedAnswers, cancellationToken);

                if (verdict.Item is not null)
                {
                    work.Items.Add(verdict.Item);
                }
                else
                {
                    work.HasLiveWork = true;
                    work.ProviderConfirmsLiveCall |= verdict.ProviderConfirmsLive;
                }
            }
        }

        // Without live work the rest is judged on what stands behind it, after the grace period. With live work the
        // agent is rightly busy, and none of it is stale.
        if (!work.HasLiveWork)
        {
            await JudgeRoutingStateAsync(agent, snapshot, work, now, cancellationToken);
        }

        return work;
    }

    private async Task JudgeRoutingStateAsync(
        AgentProfile agent,
        Snapshot snapshot,
        StaleAgentWork work,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var grace = _availabilityOptions.OrphanedBusyGracePeriod;
        var pending = _reservationManager is null
            ? null
            : await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken);

        if (!string.IsNullOrEmpty(agent.ActiveReservationId) && _reservationManager is not null)
        {
            var reservation = await _reservationManager.FindByIdAsync(agent.ActiveReservationId, cancellationToken);
            var settledSince = reservation is null ? DateTime.MinValue : reservation.ModifiedUtc ?? reservation.CreatedUtc;
            var ringing = reservation is not null &&
                reservation.Status == ReservationStatus.Pending &&
                reservation.ExpiresUtc + grace > now;

            if (!ringing && settledSince + grace <= now)
            {
                work.Items.Add(new StaleAgentWorkItem
                {
                    Kind = StaleAgentWorkKind.Reservation,
                    Reason = ReservationReason,
                    ReferenceId = agent.ActiveReservationId,
                    StartedUtc = reservation?.CreatedUtc,
                });
            }
        }

        if (snapshot.QueueItems.TryGetValue(agent.ItemId, out var queueItems))
        {
            foreach (var queueItem in queueItems)
            {
                var since = queueItem.ModifiedUtc ?? queueItem.QueueEnteredUtc;
                var offerIsLive = queueItem.Status == QueueItemStatus.Reserved &&
                    pending is not null &&
                    string.Equals(pending.QueueItemId, queueItem.ItemId, StringComparison.Ordinal) &&
                    pending.ExpiresUtc + grace > now;

                if (!offerIsLive && since + grace <= now)
                {
                    work.Items.Add(new StaleAgentWorkItem
                    {
                        Kind = StaleAgentWorkKind.QueueItem,
                        Reason = QueueItemReason,
                        ReferenceId = queueItem.ItemId,
                        StartedUtc = queueItem.QueueEnteredUtc == default ? null : queueItem.QueueEnteredUtc,
                    });
                }
            }
        }

        // A Busy or Reserved state is explained by anything already found; otherwise it has nothing behind it.
        if (work.Items.Count == 0 &&
            agent.PresenceStatus is AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy &&
            (agent.PresenceChangedUtc ?? DateTime.MinValue) + grace <= now &&
            pending is null)
        {
            work.Items.Add(new StaleAgentWorkItem
            {
                Kind = StaleAgentWorkKind.Presence,
                Reason = PresenceReason,
                StartedUtc = agent.PresenceChangedUtc,
            });
        }
    }

    // A call is stale only with evidence; every other call is live work, whether or not it is confirmed.
    private async Task<(StaleAgentWorkItem Item, bool ProviderConfirmsLive)> JudgeCallAsync(
        TelephonyInteraction call,
        bool sessionIsLive,
        DateTime now,
        bool useCachedAnswers,
        CancellationToken cancellationToken)
    {
        var started = call.StartedUtc == default ? (DateTime?)null : call.StartedUtc;
        var age = started.HasValue ? now - started.Value : TimeSpan.MaxValue;

        if (age < _options.CallMinimumAge)
        {
            return (null, false);
        }

        StaleAgentWorkItem Stale(string reason) => new()
        {
            Kind = StaleAgentWorkKind.TelephonyCall,
            Reason = reason,
            ReferenceId = call.InteractionId,
            StartedUtc = started,
        };

        if (string.IsNullOrWhiteSpace(call.ProviderName))
        {
            // A call the browser placed itself: only the browser's reports say it is up.
            var lastHeard = call.TryGet<ClientRecordedCallActivity>(out var activity) && activity.LastReportedUtc > call.StartedUtc
                ? activity.LastReportedUtc
                : call.StartedUtc;
            var silent = lastHeard == default || now - lastHeard >= _options.ClientRecordedCallSilence;

            return !sessionIsLive && (silent || age >= _options.CallMaximumAge)
                ? (Stale(AbandonedCallReason), false)
                : (null, false);
        }

        switch (await ResolveCallAsync(call.ProviderName, call.CallId, useCachedAnswers, cancellationToken))
        {
            case CallTruth.Gone:
                return (Stale(TelephonyCallReason), false);
            case CallTruth.Unresolved:
                return age >= _options.UnresolvedCallAge || (!sessionIsLive && age >= _options.CallMaximumAge)
                    ? (Stale(UnresolvedCallReason), false)
                    : (null, false);
            default:
                return (null, true);
        }
    }

    private async Task<(StaleAgentWorkItem Item, bool ProviderConfirmsLive)> JudgeInteractionAsync(
        Interaction interaction,
        DateTime now,
        bool useCachedAnswers,
        CancellationToken cancellationToken)
    {
        if (interaction.IsSettled ||
            string.IsNullOrWhiteSpace(interaction.ProviderName) ||
            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            return (null, false);
        }

        var started = interaction.StartedUtc ?? interaction.CreatedUtc;

        if (started != default && now - started < _options.CallMinimumAge)
        {
            return (null, false);
        }

        return await ResolveCallAsync(interaction.ProviderName, interaction.ProviderInteractionId, useCachedAnswers, cancellationToken) switch
        {
            CallTruth.Gone => (new StaleAgentWorkItem
            {
                Kind = StaleAgentWorkKind.ProviderInteraction,
                Reason = ProviderInteractionReason,
                ReferenceId = interaction.ItemId,
                StartedUtc = started == default ? null : started,
            }, false),
            CallTruth.Live => (null, true),
            _ => (null, false),
        };
    }

    private async Task<CallTruth> ResolveCallAsync(string providerName, string callId, bool useCachedAnswers, CancellationToken cancellationToken)
    {
        if (_providerResolver is null || string.IsNullOrWhiteSpace(callId))
        {
            return CallTruth.Unresolved;
        }

        var key = $"crestapps:cc:stale-agent-work:{_tenantName}:{providerName}:{callId}";

        if (useCachedAnswers)
        {
            var cached = await _cache.GetAsync(key, cancellationToken);

            if (cached is { Length: 1 } && Enum.IsDefined(typeof(CallTruth), (int)cached[0]))
            {
                return (CallTruth)cached[0];
            }
        }

        var truth = await LookUpCallAsync(providerName, callId, cancellationToken);

        await _cache.SetAsync(key, [(byte)truth], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.ProviderAnswerCacheDuration,
        }, cancellationToken);

        return truth;
    }

    private async Task<CallTruth> LookUpCallAsync(string providerName, string callId, CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _providerResolver.GetAsync(providerName) ?? await _providerResolver.GetAsync();

            if (provider is not ITelephonyCallStateProvider stateProvider)
            {
                return CallTruth.Unresolved;
            }

            // An unreachable provider must not hold up the dashboard's poll: past the timeout it is unresolved.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ProviderLookupTimeout);

            var lookup = await stateProvider.GetCallStateAsync(callId, timeout.Token);

            if (lookup is null || !lookup.Succeeded)
            {
                return CallTruth.Unresolved;
            }

            if (!lookup.Found)
            {
                return CallTruth.Gone;
            }

            return lookup.Call is null
                ? CallTruth.Unresolved
                : lookup.Call.State is CallState.Disconnected or CallState.Failed ? CallTruth.Gone : CallTruth.Live;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "The provider could not be asked about call '{CallId}'; it is treated as unresolved.",
                callId.SanitizeLogValue());

            return CallTruth.Unresolved;
        }
    }

    private TimeSpan GetHeartbeatTimeout() => _availabilityOptions.HeartbeatTimeout;

    // How many active phone calls are read to find the agents on one.
    private const int ActiveCallScanLimit = 500;

    private enum CallTruth
    {
        Live,
        Gone,
        Unresolved,
    }

    private sealed class Snapshot
    {
        public Dictionary<string, AgentSession> Sessions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<TelephonyInteraction>> Calls { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<Interaction>> Interactions { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<QueueItem>> QueueItems { get; } = new(StringComparer.Ordinal);
    }
}
