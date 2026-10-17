using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Works out why an agent is, or is not, getting calls, from what the agent availability report read.
/// </summary>
/// <remarks>
/// Nothing here decides anew who may take work. The availability gate is <see cref="AgentRoutingEligibility"/>, which
/// routing itself decides with; the strategies' verdicts are the candidates of a decision routing made; stale work is
/// what the live dashboard's detection found; and the recorded history is the timeline the workforce reports count from.
/// This class only lines them up per agent.
/// </remarks>
internal static class AgentAvailabilityDiagnostics
{
    /// <summary>
    /// Gets every queue the agent could be asked about: the queues and campaigns they are entitled to or signed in to.
    /// A campaign is named by its virtual campaign queue identifier.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <returns>The queue identifiers, each once.</returns>
    public static IReadOnlyList<string> GetQueueIds(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return (agent.QueueIds ?? [])
            .Concat(agent.AllowedQueueIds ?? [])
            .Concat((agent.CampaignIds ?? []).Concat(agent.AllowedCampaignIds ?? [])
                .Where(campaignId => !string.IsNullOrWhiteSpace(campaignId))
                .Select(ContactCenterConstants.CampaignQueue.CreateId))
            .Where(queueId => !string.IsNullOrWhiteSpace(queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Gets the queues the live dashboard places the agent under: the queues they are signed in to, and one queue per
    /// campaign they are signed in to, whether sign-in recorded it among their queues or only among their campaigns.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <returns>The queue identifiers.</returns>
    public static IEnumerable<string> GetSignedInQueueIds(AgentProfile agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return (agent.QueueIds ?? [])
            .Concat((agent.CampaignIds ?? [])
                .Where(campaignId => !string.IsNullOrWhiteSpace(campaignId))
                .Select(ContactCenterConstants.CampaignQueue.CreateId))
            .Where(queueId => !string.IsNullOrWhiteSpace(queueId));
    }

    /// <summary>
    /// Works out one agent's diagnosis.
    /// </summary>
    /// <param name="agent">The agent profile.</param>
    /// <param name="session">The agent's session, or <see langword="null"/> when they have none.</param>
    /// <param name="activeInteractionCount">How many interactions the agent handles now.</param>
    /// <param name="staleWork">The stale work found for the agent, or <see langword="null"/> when none was found.</param>
    /// <param name="staleWorkChecked">Whether stale work was looked for at all.</param>
    /// <param name="timeline">The agent's recorded state changes since before the start of the day, or <see langword="null"/>
    /// when none is recorded.</param>
    /// <param name="queues">What was read about each queue and campaign, by queue identifier.</param>
    /// <param name="dashboardQueueIds">The queues and campaign queues the live dashboard shows the person running the
    /// report.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="startOfDayUtc">The start of the tenant's day.</param>
    /// <param name="liveAfterUtc">The oldest heartbeat that still counts as live.</param>
    /// <returns>The diagnosis.</returns>
    public static AgentAvailabilityDiagnosis Diagnose(
        AgentProfile agent,
        AgentSession session,
        int activeInteractionCount,
        StaleAgentWork staleWork,
        bool staleWorkChecked,
        AgentStateTimeline timeline,
        IReadOnlyDictionary<string, AgentAvailabilityQueueContext> queues,
        IReadOnlySet<string> dashboardQueueIds,
        DateTime nowUtc,
        DateTime startOfDayUtc,
        DateTime liveAfterUtc)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(queues);
        ArgumentNullException.ThrowIfNull(dashboardQueueIds);

        var sessionLive = AgentRoutingEligibility.IsSessionLive(session, liveAfterUtc);
        var (callsToday, longestWaitSeconds, lastCallEndedUtc) = MeasureCalls(timeline, startOfDayUtc, nowUtc);
        var recorded = timeline?.Effective.LastOrDefault(transition => transition.ChangedUtc <= nowUtc);
        var signedInQueueIds = GetSignedInQueueIds(agent).ToArray();

        return new AgentAvailabilityDiagnosis
        {
            Agent = agent,
            AvailableSinceUtc = agent.PresenceStatus == AgentPresenceStatus.Available
                ? agent.IdleSinceUtc ?? agent.PresenceChangedUtc
                : null,
            LastCallAssignedUtc = agent.LastAssignedUtc,
            LastCallEndedUtc = lastCallEndedUtc ?? agent.LastWorkCompletedUtc,
            CallsToday = callsToday,
            LongestWaitSeconds = longestWaitSeconds,
            SignedInQueues = signedInQueueIds
                .Where(queueId => !ContactCenterConstants.IsCampaignQueue(queueId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(queueId => NameOf(queues, queueId))
                .ToArray(),
            SignedInCampaigns = signedInQueueIds
                .Where(ContactCenterConstants.IsCampaignQueue)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(queueId => NameOf(queues, queueId))
                .ToArray(),
            HasSession = session is not null,
            SessionLive = sessionLive,
            LastHeartbeatUtc = session?.LastHeartbeatUtc,
            SignedInAnywhere = signedInQueueIds.Length > 0,
            OnDashboard = signedInQueueIds.Any(dashboardQueueIds.Contains),
            Flags = GetFlags(agent, session, sessionLive, staleWork, staleWorkChecked),
            HistoryDisagreement = GetHistoryDisagreement(agent, sessionLive, recorded),
            RecordedState = recorded?.CurrentState,
            RecordedSinceUtc = recorded?.ChangedUtc,
            Queues = GetQueueIds(agent)
                .Select(queueId => GetEligibility(agent, session, activeInteractionCount, queueId, queues, liveAfterUtc))
                .OrderBy(eligibility => eligibility.IsCampaign)
                .ThenBy(eligibility => eligibility.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
        };
    }

    /// <summary>
    /// Measures an agent's calls from their recorded state changes.
    /// </summary>
    /// <param name="timeline">The agent's recorded state changes, or <see langword="null"/>.</param>
    /// <param name="startOfDayUtc">The start of the tenant's day.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The calls taken today (each change into Busy), the longest stretch today the agent was ready for a call
    /// without one in seconds, and when the last recorded call ended.</returns>
    internal static (int CallsToday, double LongestWaitSeconds, DateTime? LastCallEndedUtc) MeasureCalls(
        AgentStateTimeline timeline,
        DateTime startOfDayUtc,
        DateTime nowUtc)
    {
        if (timeline is null)
        {
            return (0, 0d, null);
        }

        var calls = 0;
        DateTime? lastCallEndedUtc = null;
        AgentPresenceStatus? prior = null;

        foreach (var transition in timeline.Effective)
        {
            if (transition.ChangedUtc > nowUtc)
            {
                break;
            }

            var previous = prior ?? transition.PreviousState;

            if (transition.CurrentState == AgentPresenceStatus.Busy &&
                previous != AgentPresenceStatus.Busy &&
                transition.ChangedUtc >= startOfDayUtc)
            {
                calls++;
            }

            if (previous == AgentPresenceStatus.Busy && transition.CurrentState != AgentPresenceStatus.Busy)
            {
                lastCallEndedUtc = transition.ChangedUtc;
            }

            prior = transition.CurrentState;
        }

        var longest = 0d;
        var running = 0d;
        DateTime? runEndUtc = null;

        foreach (var interval in AgentStateTimeline.BuildIntervals(new[] { timeline }, startOfDayUtc, nowUtc))
        {
            if (interval.Status is not (AgentPresenceStatus.Available or AgentPresenceStatus.Reserved))
            {
                running = 0d;
                runEndUtc = null;

                continue;
            }

            running = runEndUtc == interval.StartUtc
                ? running + interval.DurationSeconds
                : interval.DurationSeconds;
            runEndUtc = interval.EndUtc;
            longest = Math.Max(longest, running);
        }

        return (calls, longest, lastCallEndedUtc);
    }

    private static List<AgentAvailabilityFlag> GetFlags(
        AgentProfile agent,
        AgentSession session,
        bool sessionLive,
        StaleAgentWork staleWork,
        bool staleWorkChecked)
    {
        var flags = new List<AgentAvailabilityFlag>();

        if (agent.PresenceStatus == AgentPresenceStatus.Available && !sessionLive)
        {
            flags.Add(new AgentAvailabilityFlag(AgentAvailabilityFlagKind.AvailableWithoutLiveSession, null, session?.LastHeartbeatUtc));
        }

        foreach (var item in staleWork?.Items ?? [])
        {
            var kind = item.Kind switch
            {
                StaleAgentWorkKind.TelephonyCall => AgentAvailabilityFlagKind.UnresolvedSoftPhoneCall,
                StaleAgentWorkKind.ProviderInteraction => AgentAvailabilityFlagKind.OpenInteractionAfterCallEnded,
                StaleAgentWorkKind.Reservation => AgentAvailabilityFlagKind.StaleReservation,
                StaleAgentWorkKind.QueueItem => AgentAvailabilityFlagKind.StaleAssignedWork,
                _ => agent.PresenceStatus == AgentPresenceStatus.Reserved
                    ? AgentAvailabilityFlagKind.ReservedWithoutLiveInteraction
                    : AgentAvailabilityFlagKind.BusyWithoutLiveInteraction,
            };

            flags.Add(new AgentAvailabilityFlag(kind, item.Reason, item.StartedUtc));
        }

        if (!string.IsNullOrEmpty(agent.ActiveReservationId) &&
            !flags.Any(flag => flag.Kind == AgentAvailabilityFlagKind.StaleReservation))
        {
            flags.Add(new AgentAvailabilityFlag(AgentAvailabilityFlagKind.HoldsReservation, agent.ActiveReservationId, null));
        }

        if (!staleWorkChecked)
        {
            flags.Add(new AgentAvailabilityFlag(AgentAvailabilityFlagKind.StaleWorkNotChecked, null, null));
        }

        return flags;
    }

    private static AgentHistoryDisagreement GetHistoryDisagreement(
        AgentProfile agent,
        bool sessionLive,
        AgentStateTransition recorded)
    {
        if (recorded is null)
        {
            return agent.PresenceStatus == AgentPresenceStatus.Offline
                ? AgentHistoryDisagreement.None
                : AgentHistoryDisagreement.NoRecordedState;
        }

        var result = AgentHistoryDisagreement.None;

        if (recorded.CurrentState != AgentPresenceStatus.Offline && !sessionLive)
        {
            result |= AgentHistoryDisagreement.CountedSignedInWithoutLiveSession;
        }

        if (recorded.CurrentState != agent.PresenceStatus)
        {
            result |= AgentHistoryDisagreement.StateDiffers;
        }

        return result;
    }

    private static AgentQueueEligibility GetEligibility(
        AgentProfile agent,
        AgentSession session,
        int activeInteractionCount,
        string queueId,
        IReadOnlyDictionary<string, AgentAvailabilityQueueContext> queues,
        DateTime liveAfterUtc)
    {
        queues.TryGetValue(queueId, out var context);

        var campaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(queueId);
        var isCampaign = campaignId is not null;
        var entitledToCampaign = isCampaign &&
            agent.AllowedCampaignIds?.Contains(campaignId, StringComparer.OrdinalIgnoreCase) == true;
        var gate = AgentRoutingEligibility.Evaluate(agent, session, queueId, activeInteractionCount, liveAfterUtc);

        // A campaign's own queue joins the agent's entitlements when they sign in to the campaign, so an agent entitled
        // to the campaign who is not signed in to it is told they are not signed in, not that they are not entitled.
        if (entitledToCampaign && (gate & AgentRoutingIneligibility.NotSignedIn) != 0)
        {
            gate &= ~AgentRoutingIneligibility.NotEntitled;
        }

        var blocker = context?.Blocker ?? AgentQueueBlocker.None;
        var candidate = blocker == AgentQueueBlocker.None && gate == AgentRoutingIneligibility.None
            ? context?.Decision?.Candidates.FirstOrDefault(entry => string.Equals(entry.Agent?.ItemId, agent.ItemId, StringComparison.Ordinal))
            : null;
        var routingEvaluated = blocker == AgentQueueBlocker.None &&
            gate == AgentRoutingIneligibility.None &&
            context?.Decision is not null;

        return new AgentQueueEligibility
        {
            QueueId = queueId,
            Name = context?.Name ?? (isCampaign ? campaignId : queueId),
            IsCampaign = isCampaign,
            Entitled = entitledToCampaign || agent.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true,
            SignedIn = agent.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true ||
                (isCampaign && agent.CampaignIds?.Contains(campaignId, StringComparer.OrdinalIgnoreCase) == true),
            Blocker = blocker,
            Gate = gate,
            ActiveInteractionCount = activeInteractionCount,
            Capacity = AgentRoutingEligibility.GetCapacity(agent),
            WaitingCount = context?.WaitingCount ?? 0,
            RoutingEvaluated = routingEvaluated,
            RoutingEligible = candidate?.IsEligible,
            NextInLine = candidate is not null &&
                context.Decision.Succeeded &&
                string.Equals(context.Decision.Agent?.ItemId, agent.ItemId, StringComparison.Ordinal),
            RoutingReasons = candidate?.Reasons.ToArray() ?? [],
        };
    }

    private static string NameOf(IReadOnlyDictionary<string, AgentAvailabilityQueueContext> queues, string queueId)
        => queues.TryGetValue(queueId, out var context) && !string.IsNullOrEmpty(context.Name)
            ? context.Name
            : ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) ?? queueId;
}
