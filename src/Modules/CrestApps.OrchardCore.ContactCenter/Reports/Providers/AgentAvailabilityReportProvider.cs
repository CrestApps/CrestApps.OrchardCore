using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore.Localization;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The live agent availability and wait-time report: for each agent, how long they have waited for a call, what they took
/// today, whether their session is live, what is stale, whether their recorded history agrees with their profile, and,
/// for each queue and campaign, why they are or are not being given work.
/// </summary>
/// <remarks>
/// <para>
/// The report answers "why is this agent not getting calls?" with the reasons the platform acts on, not with a second
/// opinion: the availability gate is the one routing decides with, the strategies' verdicts come from asking routing
/// about each queue's next waiting item, stale work is what the live dashboard's detection finds, and the recorded
/// history is the timeline the workforce reports count from.
/// </para>
/// <para>
/// The live dashboard lists only agents signed in to a queue or campaign the supervisor oversees. This report lists
/// every agent entitled to or signed in to one of them, so an agent the dashboard leaves out (signed in nowhere, or
/// counted as Available with no session) is shown, with the reason the dashboard leaves them out.
/// </para>
/// </remarks>
public sealed class AgentAvailabilityReportProvider : ContactCenterReportBase, IReport
{
    private const string FlagColor = "#B42318";

    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentSessionManager _sessionManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IInteractionEventStore _eventStore;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IBusinessHoursService _businessHours;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityRoutingService _routingService;
    private readonly ISupervisorQueueAuthorizationService _supervisorQueueAuthorization;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IStaleAgentWorkService _staleWorkService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AgentAvailabilityOptions _availabilityOptions;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentAvailabilityReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="agentManager">The agent profiles.</param>
    /// <param name="sessionManager">The agents' live sessions.</param>
    /// <param name="interactionManager">The interactions, for each agent's active count.</param>
    /// <param name="eventStore">The event log, for each agent's recorded state history.</param>
    /// <param name="queueManager">The stored queues.</param>
    /// <param name="queueItemManager">The items waiting in each queue.</param>
    /// <param name="businessHours">The business hours that pause a closed queue.</param>
    /// <param name="availabilityService">The availability gate routing reads its candidates from.</param>
    /// <param name="routingService">The routing service, asked about each queue's next waiting item.</param>
    /// <param name="supervisorQueueAuthorization">The check the live dashboard scopes its queues and agents with.</param>
    /// <param name="campaignManager">The campaigns, for their names.</param>
    /// <param name="staleWorkServices">The stale-work detection, registered only with the live dashboard.</param>
    /// <param name="httpContextAccessor">The accessor of the request, for who is running the report.</param>
    /// <param name="availabilityOptions">The availability policy, whose heartbeat timeout decides a live session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the day is in.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AgentAvailabilityReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IAgentProfileManager agentManager,
        IAgentSessionManager sessionManager,
        IInteractionManager interactionManager,
        IInteractionEventStore eventStore,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IBusinessHoursService businessHours,
        IAgentAvailabilityService availabilityService,
        IActivityRoutingService routingService,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorization,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IEnumerable<IStaleAgentWorkService> staleWorkServices,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AgentAvailabilityOptions> availabilityOptions,
        IClock clock,
        ILocalClock localClock,
        IStringLocalizer<AgentAvailabilityReportProvider> stringLocalizer)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _agentManager = agentManager;
        _sessionManager = sessionManager;
        _interactionManager = interactionManager;
        _eventStore = eventStore;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _businessHours = businessHours;
        _availabilityService = availabilityService;
        _routingService = routingService;
        _supervisorQueueAuthorization = supervisorQueueAuthorization;
        _campaignManager = campaignManager;
        _staleWorkService = staleWorkServices?.FirstOrDefault();
        _httpContextAccessor = httpContextAccessor;
        _availabilityOptions = availabilityOptions.Value;
        _clock = clock;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-agent-availability";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AgentAvailabilityReportProvider>("Agent availability and wait time");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AgentAvailabilityReportProvider>("Live, per agent: how long each has waited for a call, today's calls, session health, stale states, and why each queue or campaign is or is not giving them work.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.QueueRouting;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } =
    [
        ContactCenterReportFilter.AgentId,
    ];

    /// <inheritdoc/>
    /// <remarks>Agents, sessions, queues and routing belong to the capabilities the reporting feature already depends on.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } = [];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);
        var startOfDay = timeZone.StartOfDayUtc(now);
        var liveAfter = now - _availabilityOptions.HeartbeatTimeout;
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);
        var allAgents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => agent is not null && !string.IsNullOrEmpty(agent.ItemId))
            .ToArray();

        // Who is running the report decides which queues they oversee, exactly as the live dashboard decides it.
        var principal = _httpContextAccessor.HttpContext?.User;
        var viewerId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var overseen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (principal is not null && !string.IsNullOrEmpty(viewerId))
        {
            foreach (var queueId in allAgents.SelectMany(AgentAvailabilityDiagnostics.GetQueueIds).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (await _supervisorQueueAuthorization.IsAuthorizedAsync(principal, viewerId, queueId, cancellationToken))
                {
                    overseen.Add(queueId);
                }
            }
        }

        var scope = !string.IsNullOrEmpty(criteria.AgentId)
            ? AgentAvailabilityReportScope.ChosenAgent
            : overseen.Count > 0
                ? AgentAvailabilityReportScope.Overseen
                : AgentAvailabilityReportScope.Everyone;

        // An agent entitled to an overseen queue is listed whether or not they are signed in to it: an agent the dashboard
        // leaves out is exactly the one this report has to be able to explain.
        var agents = scope switch
        {
            AgentAvailabilityReportScope.ChosenAgent => allAgents
                .Where(agent => string.Equals(agent.ItemId, criteria.AgentId, StringComparison.Ordinal))
                .ToArray(),
            AgentAvailabilityReportScope.Overseen => allAgents
                .Where(agent => AgentAvailabilityDiagnostics.GetQueueIds(agent).Any(overseen.Contains))
                .ToArray(),
            _ => allAgents,
        };

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

        var staleWork = _staleWorkService is null || agents.Length == 0
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

        return Build(diagnoses, now, startOfDay, timeZone.Name, scope);
    }

    /// <summary>
    /// Renders the report.
    /// </summary>
    /// <param name="diagnoses">The agents' diagnoses.</param>
    /// <param name="nowUtc">The time the report was read.</param>
    /// <param name="startOfDayUtc">The start of the tenant's day.</param>
    /// <param name="timeZoneName">The name of the tenant's time zone.</param>
    /// <param name="scope">Which agents the report lists.</param>
    /// <returns>The report document.</returns>
    internal ReportDocument Build(
        IReadOnlyList<AgentAvailabilityDiagnosis> diagnoses,
        DateTime nowUtc,
        DateTime startOfDayUtc,
        string timeZoneName,
        AgentAvailabilityReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(diagnoses);

        var ordered = diagnoses
            .OrderBy(diagnosis => diagnosis.AvailableSinceUtc.HasValue ? 0 : 1)
            .ThenBy(diagnosis => diagnosis.AvailableSinceUtc ?? DateTime.MaxValue)
            .ThenBy(diagnosis => diagnosis.Agent.UserName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Agents"].Value, ReportFormat.Number(ordered.Length)),
            new ReportMetric(S["Available"].Value, ReportFormat.Number(ordered.Count(diagnosis => diagnosis.Agent.PresenceStatus == AgentPresenceStatus.Available))),
            new ReportMetric(S["Available with no live session"].Value, ReportFormat.Number(ordered.Count(diagnosis => diagnosis.Flags.Any(flag => flag.Kind == AgentAvailabilityFlagKind.AvailableWithoutLiveSession)))),
            new ReportMetric(S["With a stale state"].Value, ReportFormat.Number(ordered.Count(diagnosis => diagnosis.Flags.Any(IsStale)))),
            new ReportMetric(S["History disagrees with profile"].Value, ReportFormat.Number(ordered.Count(diagnosis => diagnosis.HistoryDisagreement != AgentHistoryDisagreement.None))),
            new ReportMetric(S["Not on your live dashboard"].Value, ReportFormat.Number(ordered.Count(diagnosis => !diagnosis.OnDashboard))),
            new ReportMetric(S["Eligible for work now"].Value, ReportFormat.Number(ordered.Count(diagnosis => diagnosis.Queues.Any(queue => queue.Eligible)))),
        ]);

        summary.Description = string.Join(" ",
            S["Live as of {0} UTC. Today started at {1} UTC ({2}). The date range is not used.", AuditReportFormat.Timestamp(nowUtc), AuditReportFormat.Timestamp(startOfDayUtc), timeZoneName].Value,
            scope switch
            {
                AgentAvailabilityReportScope.ChosenAgent => S["Shows the agent you chose."].Value,
                AgentAvailabilityReportScope.Overseen => S["Shows every agent entitled to or signed in to a queue or campaign you oversee, including agents the live dashboard leaves out."].Value,
                _ => S["Shows every agent."].Value,
            });

        return new ReportDocument()
            .Add(summary)
            .Add(BuildAgents(ordered, nowUtc))
            .Add(BuildQueues(ordered));
    }

    private ReportSection BuildAgents(IReadOnlyList<AgentAvailabilityDiagnosis> diagnoses, DateTime nowUtc)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Presence"].Value),
            new ReportColumn(S["Available since (UTC)"].Value),
            new ReportColumn(S["Idle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Last call assigned (UTC)"].Value),
            new ReportColumn(S["Last call ended (UTC)"].Value),
            new ReportColumn(S["Calls today"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Longest wait between calls"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Signed in to"].Value),
            new ReportColumn(S["Live session"].Value),
            new ReportColumn(S["Heartbeat age"].Value, ReportColumnAlign.End),
            new ReportColumn(S["On live dashboard"].Value),
            new ReportColumn(S["Stale-state flags"].Value),
            new ReportColumn(S["History check"].Value),
        };

        var rows = diagnoses
            .Select(diagnosis =>
            {
                var agent = diagnosis.Agent;
                var flags = diagnosis.Flags.Select(DescribeFlag).ToArray();
                var history = DescribeHistory(diagnosis);
                var row = new ReportRow(
                [
                    AgentName(agent),
                    string.IsNullOrWhiteSpace(agent.PresenceReason)
                        ? agent.PresenceStatus.ToString()
                        : S["{0} ({1})", agent.PresenceStatus.ToString(), agent.PresenceReason].Value,
                    Timestamp(diagnosis.AvailableSinceUtc),
                    diagnosis.AvailableSinceUtc.HasValue ? Age(nowUtc, diagnosis.AvailableSinceUtc.Value) : "—",
                    Timestamp(diagnosis.LastCallAssignedUtc),
                    Timestamp(diagnosis.LastCallEndedUtc),
                    ReportFormat.Number(diagnosis.CallsToday),
                    AuditReportFormat.Clock(diagnosis.LongestWaitSeconds),
                    DescribeSignedIn(diagnosis),
                    diagnosis.SessionLive
                        ? S["Yes"].Value
                        : diagnosis.HasSession
                            ? S["No: offline, disconnected or no recent heartbeat"].Value
                            : S["No session"].Value,
                    diagnosis.LastHeartbeatUtc.HasValue ? Age(nowUtc, diagnosis.LastHeartbeatUtc.Value) : "—",
                    diagnosis.OnDashboard
                        ? S["Yes"].Value
                        : diagnosis.SignedInAnywhere
                            ? S["No: not signed in to a queue or campaign you oversee"].Value
                            : S["No: not signed in to any queue or campaign"].Value,
                    flags.Length == 0 ? "—" : string.Join(" ", flags),
                    history.Count == 0 ? "—" : string.Join(" ", history),
                ]);

                if (diagnosis.Flags.Any(IsStale))
                {
                    row = row.WithCellStyle(12, ReportStyle.Create(color: FlagColor, bold: true));
                }

                if (history.Count > 0)
                {
                    row = row.WithCellStyle(13, ReportStyle.Create(color: FlagColor, bold: true));
                }

                return row;
            })
            .ToList();

        return ReportSection.ForTable(S["Agents"].Value, columns, rows);
    }

    private ReportSection BuildQueues(IReadOnlyList<AgentAvailabilityDiagnosis> diagnoses)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Queue or campaign"].Value),
            new ReportColumn(S["Type"].Value),
            new ReportColumn(S["Entitled"].Value),
            new ReportColumn(S["Signed in"].Value),
            new ReportColumn(S["Waiting"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Eligible now"].Value),
            new ReportColumn(S["Why not eligible"].Value),
            new ReportColumn(S["Routing detail"].Value),
        };

        var rows = new List<ReportRow>();

        foreach (var diagnosis in diagnoses)
        {
            var name = AgentName(diagnosis.Agent);

            if (diagnosis.Queues.Count == 0)
            {
                rows.Add(new ReportRow(
                [
                    name,
                    "—",
                    "—",
                    S["No"].Value,
                    S["No"].Value,
                    "—",
                    S["No"].Value,
                    S["Not entitled to or signed in to any queue or campaign."].Value,
                    "—",
                ]).WithCellStyle(7, ReportStyle.Create(color: FlagColor, bold: true)));

                continue;
            }

            foreach (var queue in diagnosis.Queues)
            {
                var row = new ReportRow(
                [
                    name,
                    queue.Name,
                    queue.IsCampaign ? S["Campaign"].Value : S["Queue"].Value,
                    queue.Entitled ? S["Yes"].Value : S["No"].Value,
                    queue.SignedIn ? S["Yes"].Value : S["No"].Value,
                    ReportFormat.Number(queue.WaitingCount),
                    queue.Eligible ? S["Yes"].Value : S["No"].Value,
                    string.Join(" ", DescribeEligibility(diagnosis.Agent, queue)),
                    queue.RoutingEvaluated && queue.RoutingReasons.Count > 0
                        ? string.Join(" ", queue.RoutingReasons)
                        : "—",
                ]);

                rows.Add(queue.Eligible
                    ? row
                    : row.WithCellStyle(7, ReportStyle.Create(color: FlagColor, bold: true)));
            }
        }

        var section = ReportSection.ForTable(S["Why each queue or campaign is or is not giving work"].Value, columns, rows);
        section.Description = S["Each reason is the one the platform acts on: the availability gate routing checks first, then the verdict routing gives for the next waiting item."].Value;

        return section;
    }

    private List<string> DescribeEligibility(AgentProfile agent, AgentQueueEligibility queue)
    {
        var reasons = new List<string>();

        switch (queue.Blocker)
        {
            case AgentQueueBlocker.Missing:
                reasons.Add(S["The queue no longer exists."].Value);
                break;
            case AgentQueueBlocker.Disabled:
                reasons.Add(S["The queue is turned off."].Value);
                break;
            case AgentQueueBlocker.Closed:
                reasons.Add(S["Closed by its business hours."].Value);
                break;
        }

        if ((queue.Gate & AgentRoutingIneligibility.NotEntitled) != 0)
        {
            reasons.Add(queue.IsCampaign ? S["Not entitled to this campaign."].Value : S["Not entitled to this queue."].Value);
        }

        if ((queue.Gate & AgentRoutingIneligibility.NotSignedIn) != 0)
        {
            reasons.Add(queue.IsCampaign ? S["Not signed in to this campaign."].Value : S["Not signed in to this queue."].Value);
        }

        if ((queue.Gate & AgentRoutingIneligibility.NoLiveSession) != 0)
        {
            reasons.Add(S["No live session."].Value);
        }
        else if ((queue.Gate & AgentRoutingIneligibility.SessionNotSignedIn) != 0)
        {
            reasons.Add(S["The live session is not signed in to it."].Value);
        }

        if ((queue.Gate & AgentRoutingIneligibility.NotAvailable) != 0)
        {
            reasons.Add(S["Not Available (presence is {0}).", agent.PresenceStatus.ToString()].Value);
        }

        if ((queue.Gate & AgentRoutingIneligibility.AtCapacity) != 0)
        {
            reasons.Add(S["At capacity ({0}/{1} active interactions).", queue.ActiveInteractionCount, queue.Capacity].Value);
        }

        if (reasons.Count > 0)
        {
            return reasons;
        }

        if (queue.RoutingEvaluated && queue.RoutingEligible == false)
        {
            reasons.Add(S["Routing passes this agent over for the next waiting item; the routing detail says why, for example a missing skill or having already had the call."].Value);
        }
        else if (queue.RoutingEvaluated && queue.RoutingEligible is null)
        {
            reasons.Add(S["Not among the agents routing considers for the next waiting item."].Value);
        }
        else if (queue.NextInLine)
        {
            reasons.Add(S["None: next in line for the waiting work."].Value);
        }
        else if (queue.WaitingCount == 0)
        {
            reasons.Add(S["None, but no work is waiting."].Value);
        }
        else if (queue.RoutingEvaluated)
        {
            reasons.Add(S["None: eligible, but another agent is ahead in line."].Value);
        }
        else
        {
            reasons.Add(S["None, but no waiting item can be worked now."].Value);
        }

        return reasons;
    }

    private string DescribeFlag(AgentAvailabilityFlag flag)
    {
        if (flag.Kind == AgentAvailabilityFlagKind.AvailableWithoutLiveSession)
        {
            return flag.SinceUtc.HasValue
                ? S["Available with no live session (last heartbeat {0} UTC).", AuditReportFormat.Timestamp(flag.SinceUtc.Value)].Value
                : S["Available with no live session (no session at all)."].Value;
        }

        if (flag.Kind == AgentAvailabilityFlagKind.HoldsReservation)
        {
            return S["Holds reservation {0}.", flag.Detail].Value;
        }

        var label = flag.Kind switch
        {
            AgentAvailabilityFlagKind.UnresolvedSoftPhoneCall => S["Unresolved soft-phone call record."].Value,
            AgentAvailabilityFlagKind.OpenInteractionAfterCallEnded => S["Open interaction although the provider reports the call ended."].Value,
            AgentAvailabilityFlagKind.BusyWithoutLiveInteraction => S["Busy with no live interaction."].Value,
            AgentAvailabilityFlagKind.ReservedWithoutLiveInteraction => S["Reserved with no live interaction."].Value,
            AgentAvailabilityFlagKind.StaleReservation => S["Held reservation that no offer or call backs."].Value,
            AgentAvailabilityFlagKind.StaleAssignedWork => S["Work assigned with no offer or call behind it."].Value,
            _ => S["Stale work not checked: the Contact Center Supervision & Live Dashboard feature is off."].Value,
        };

        var parts = new List<string> { label };

        if (flag.SinceUtc.HasValue)
        {
            parts.Add(S["Since {0} UTC.", AuditReportFormat.Timestamp(flag.SinceUtc.Value)].Value);
        }

        if (!string.IsNullOrWhiteSpace(flag.Detail))
        {
            parts.Add(flag.Detail);
        }

        return string.Join(" ", parts);
    }

    private List<string> DescribeHistory(AgentAvailabilityDiagnosis diagnosis)
    {
        var notes = new List<string>();
        var profileState = diagnosis.Agent.PresenceStatus.ToString();

        if ((diagnosis.HistoryDisagreement & AgentHistoryDisagreement.NoRecordedState) != 0)
        {
            notes.Add(S["No state change is recorded for this agent, so the time summary shows no time for them, although the profile says {0}.", profileState].Value);
        }

        if (!diagnosis.RecordedState.HasValue || !diagnosis.RecordedSinceUtc.HasValue)
        {
            return notes;
        }

        var recordedState = diagnosis.RecordedState.Value.ToString();
        var recordedSince = AuditReportFormat.Timestamp(diagnosis.RecordedSinceUtc.Value);

        if ((diagnosis.HistoryDisagreement & AgentHistoryDisagreement.CountedSignedInWithoutLiveSession) != 0)
        {
            notes.Add(S["No sign-out is recorded after the change to {0} at {1} UTC, so the time summary counts this agent as {0} up to now, but the agent has no live session.", recordedState, recordedSince].Value);
        }

        if ((diagnosis.HistoryDisagreement & AgentHistoryDisagreement.StateDiffers) != 0)
        {
            notes.Add(S["The profile says {0}, but the last recorded change is to {1} at {2} UTC.", profileState, recordedState, recordedSince].Value);
        }

        return notes;
    }

    private string DescribeSignedIn(AgentAvailabilityDiagnosis diagnosis)
    {
        var parts = diagnosis.SignedInQueues
            .Concat(diagnosis.SignedInCampaigns.Select(campaign => S["Campaign: {0}", campaign].Value))
            .ToArray();

        return parts.Length == 0
            ? S["Nothing"].Value
            : string.Join("; ", parts);
    }

    private string AgentName(AgentProfile agent)
        => ReportValue.UserDisplayName(agent.UserName, S["(Unknown agent)"].Value);

    private static string Timestamp(DateTime? value)
        => value.HasValue ? AuditReportFormat.Timestamp(value.Value) : "—";

    private static string Age(DateTime nowUtc, DateTime sinceUtc)
        => AuditReportFormat.Clock(Math.Max(0d, (nowUtc - sinceUtc).TotalSeconds));

    // A held reservation can be an offer ringing right now, and a report that could not look for stale work found
    // nothing either way; neither is a stale state.
    private static bool IsStale(AgentAvailabilityFlag flag)
        => flag.Kind is not (AgentAvailabilityFlagKind.HoldsReservation or AgentAvailabilityFlagKind.StaleWorkNotChecked);

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
