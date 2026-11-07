using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// What <see cref="AgentAvailabilityReader"/> read: each agent's diagnosis and the moments it was read at.
/// </summary>
/// <param name="Diagnoses">One diagnosis per agent.</param>
/// <param name="NowUtc">The time the agents were read.</param>
/// <param name="StartOfDayUtc">The start of the tenant's day.</param>
/// <param name="TimeZoneName">The name of the tenant's time zone.</param>
internal sealed record AgentAvailabilityReading(
    IReadOnlyList<AgentAvailabilityDiagnosis> Diagnoses,
    DateTime NowUtc,
    DateTime StartOfDayUtc,
    string TimeZoneName);

/// <summary>
/// Reads what is needed to say why an agent is, or is not, getting calls: the live session, the active interactions, the
/// stale work, the recorded state history, and, for each queue and campaign the agent is entitled to or signed in to,
/// the checks assignment makes and the decision routing gives for the next waiting item. The agent availability report
/// and the Contact Center AI tools both read through it, so they never disagree about the same agent.
/// </summary>
/// <remarks>
/// Nothing here decides anew who may take work. The availability gate is <see cref="AgentRoutingEligibility"/>, which
/// routing itself decides with, and the verdicts are the decision routing made when asked about the next waiting item.
/// </remarks>
public sealed class AgentAvailabilityReader
{
    private readonly IAgentSessionManager _sessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IInteractionEventStore _eventStore;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IBusinessHoursService _businessHours;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityRoutingService _routingService;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IStaleAgentWorkService _staleWorkService;
    private readonly AgentAvailabilityOptions _availabilityOptions;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityReader"/> class.
    /// </summary>
    /// <param name="sessionManager">The agents' live sessions.</param>
    /// <param name="interactionManager">The interactions, for each agent's active count.</param>
    /// <param name="eventStore">The event log, for each agent's recorded state history.</param>
    /// <param name="queueManager">The stored queues.</param>
    /// <param name="queueItemManager">The items waiting in each queue.</param>
    /// <param name="businessHours">The business hours that pause a closed queue.</param>
    /// <param name="availabilityService">The availability gate routing reads its candidates from.</param>
    /// <param name="routingService">The routing service, asked about each queue's next waiting item.</param>
    /// <param name="campaignManager">The campaigns, for their names.</param>
    /// <param name="staleWorkServices">The stale-work detection, registered only with the live dashboard.</param>
    /// <param name="availabilityOptions">The availability policy, whose heartbeat timeout decides a live session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the day is in.</param>
    public AgentAvailabilityReader(
        IAgentSessionManager sessionManager,
        IInteractionManager interactionManager,
        IInteractionEventStore eventStore,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IBusinessHoursService businessHours,
        IAgentAvailabilityService availabilityService,
        IActivityRoutingService routingService,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IEnumerable<IStaleAgentWorkService> staleWorkServices,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        IClock clock,
        ILocalClock localClock)
    {
        _sessionManager = sessionManager;
        _interactionManager = interactionManager;
        _eventStore = eventStore;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _businessHours = businessHours;
        _availabilityService = availabilityService;
        _routingService = routingService;
        _campaignManager = campaignManager;
        _staleWorkService = staleWorkServices?.FirstOrDefault();
        _availabilityOptions = availabilityOptions.Value;
        _clock = clock;
        _localClock = localClock;
    }

    /// <summary>
    /// Gets the live session timeout the reader counts a heartbeat against.
    /// </summary>
    public TimeSpan HeartbeatTimeout => _availabilityOptions.HeartbeatTimeout;

    /// <summary>
    /// Reads the supplied agents.
    /// </summary>
    /// <param name="agents">The agents to diagnose.</param>
    /// <param name="overseen">The queues and campaign queues the person asking oversees, which decide who the live
    /// dashboard would show them. An empty set says the dashboard shows them nobody.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The diagnoses.</returns>
    internal async Task<AgentAvailabilityReading> ReadAsync(
        IReadOnlyList<AgentProfile> agents,
        IReadOnlySet<string> overseen,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(overseen);

        var now = _clock.UtcNow;
        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);
        var startOfDay = timeZone.StartOfDayUtc(now);
        var liveAfter = now - _availabilityOptions.HeartbeatTimeout;
        var userIds = agents
            .Where(agent => !string.IsNullOrEmpty(agent.UserId))
            .Select(agent => agent.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var agentIds = agents.Select(agent => agent.ItemId).ToArray();
        var sessions = new Dictionary<string, AgentSession>(StringComparer.Ordinal);

        if (userIds.Length > 0)
        {
            foreach (var session in await _sessionManager.GetByUserIdsAsync(userIds, cancellationToken) ?? [])
            {
                if (session is not null && !string.IsNullOrEmpty(session.UserId))
                {
                    sessions[session.UserId] = session;
                }
            }
        }

        var activeCounts = agentIds.Length == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await _interactionManager.CountActiveByAgentIdsAsync(agentIds, cancellationToken);

        var staleWork = _staleWorkService is null || agents.Count == 0
            ? new Dictionary<string, StaleAgentWork>(StringComparer.Ordinal)
            : await _staleWorkService.DetectAsync(agents, cancellationToken);

        var timelines = agentIds.Length == 0
            ? new Dictionary<string, AgentStateTimeline>(StringComparer.Ordinal)
            : AgentStateTimeline.Build(await AgentStateEventReader.ReadAsync(
                _eventStore,
                agentIds,
                onlyTheseAgents: true,
                startOfDay,
                now,
                [],
                cancellationToken))
                .ToDictionary(timeline => timeline.AgentId, StringComparer.Ordinal);

        var queues = await ReadQueuesAsync(agents, sessions, activeCounts, now, liveAfter, cancellationToken);

        // The dashboard shows an agent under a stored queue only while the queue is enabled; a campaign's queue is never
        // stored, so it is shown whenever it is overseen.
        var dashboardQueueIds = overseen
            .Where(queueId => ContactCenterConstants.IsCampaignQueue(queueId) ||
                (queues.TryGetValue(queueId, out var queue) && queue.Blocker is not (AgentQueueBlocker.Missing or AgentQueueBlocker.Disabled)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var diagnoses = agents
            .Select(agent => AgentAvailabilityDiagnostics.Diagnose(
                agent,
                sessions.TryGetValue(agent.UserId ?? string.Empty, out var session) ? session : null,
                activeCounts.TryGetValue(agent.ItemId, out var activeCount) ? activeCount : 0,
                staleWork.TryGetValue(agent.ItemId, out var stale) ? stale : null,
                _staleWorkService is not null,
                timelines.TryGetValue(agent.ItemId, out var timeline) ? timeline : null,
                queues,
                dashboardQueueIds,
                now,
                startOfDay,
                liveAfter))
            .ToArray();

        return new AgentAvailabilityReading(diagnoses, now, startOfDay, timeZone.Name);
    }

    private async Task<Dictionary<string, AgentAvailabilityQueueContext>> ReadQueuesAsync(
        IReadOnlyCollection<AgentProfile> agents,
        IReadOnlyDictionary<string, AgentSession> sessions,
        IReadOnlyDictionary<string, int> activeCounts,
        DateTime now,
        DateTime liveAfter,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, AgentAvailabilityQueueContext>(StringComparer.OrdinalIgnoreCase);
        var queueIds = agents
            .SelectMany(AgentAvailabilityDiagnostics.GetQueueIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (queueIds.Length == 0)
        {
            return result;
        }

        var storedQueues = new Dictionary<string, ActivityQueue>(StringComparer.OrdinalIgnoreCase);

        foreach (var queue in await _queueManager.GetAllAsync(cancellationToken) ?? [])
        {
            if (queue is not null && !string.IsNullOrEmpty(queue.ItemId))
            {
                storedQueues[queue.ItemId] = queue;
            }
        }

        var campaignNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var campaign in await _campaignManager.GetAllAsync(cancellationToken) ?? [])
        {
            if (campaign is not null && !string.IsNullOrEmpty(campaign.ItemId))
            {
                campaignNames[campaign.ItemId] = string.IsNullOrWhiteSpace(campaign.DisplayText) ? campaign.ItemId : campaign.DisplayText;
            }
        }

        var waitingCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (queueId, count) in await _queueItemManager.CountWaitingByQueueIdsAsync(queueIds, cancellationToken) ?? new Dictionary<string, int>())
        {
            waitingCounts[queueId] = count;
        }

        foreach (var queueId in queueIds)
        {
            var campaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(queueId);
            var queue = campaignId is not null
                ? CampaignRoutingQueue.Create(queueId)
                : storedQueues.GetValueOrDefault(queueId);
            var name = campaignId is not null
                ? campaignNames.GetValueOrDefault(campaignId) ?? campaignId
                : queue?.Name ?? queueId;

            // The checks assignment makes before it looks at any agent, in its order.
            var blocker = queue is null
                ? AgentQueueBlocker.Missing
                : !queue.Enabled
                    ? AgentQueueBlocker.Disabled
                    : !await _businessHours.IsOpenAsync(queue.BusinessHoursCalendarId, now, cancellationToken)
                        ? AgentQueueBlocker.Closed
                        : AgentQueueBlocker.None;
            var waiting = waitingCounts.GetValueOrDefault(queueId);
            ActivityRoutingDecision decision = null;

            // Routing is asked only when its answer can matter for an agent listed here: something is waiting and one of
            // them passes the availability gate. It is asked exactly as assignment asks it, with the same candidates.
            if (blocker == AgentQueueBlocker.None &&
                waiting > 0 &&
                agents.Any(agent => AgentRoutingEligibility.Evaluate(
                    agent,
                    sessions.GetValueOrDefault(agent.UserId ?? string.Empty),
                    queueId,
                    activeCounts.GetValueOrDefault(agent.ItemId),
                    liveAfter) == AgentRoutingIneligibility.None))
            {
                var head = await _queueItemManager.FindNextWaitingAsync(queue, now, cancellationToken);

                if (head is not null)
                {
                    var availability = await _availabilityService.GetForQueueAsync(queueId, cancellationToken);

                    decision = await _routingService.SelectAgentAsync(queue, head, availability, cancellationToken);
                }
            }

            result[queueId] = new AgentAvailabilityQueueContext
            {
                QueueId = queueId,
                Name = name,
                IsCampaign = campaignId is not null,
                Blocker = blocker,
                WaitingCount = waiting,
                Decision = decision,
            };
        }

        return result;
    }
}
