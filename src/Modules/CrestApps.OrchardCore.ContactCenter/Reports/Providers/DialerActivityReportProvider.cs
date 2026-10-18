using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore.Localization;
using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The Contact Center dialer activity report: what the dialer is doing now for each paced campaign, its last pacing
/// cycle and last call per campaign queue and per dialer profile, calls and routing decisions per hour, and the attempts
/// the compliance gate suppressed, by reason.
/// </summary>
/// <remarks>
/// The report is built for telling a stalled dialer from one that is idle or has nothing to call. Each pacing cycle
/// leaves a record however it ends, so a dialer whose cycles stopped shows a last cycle that is old while work waits,
/// one whose cycles find no agent shows them completing with "No agents", and one with nothing due shows them
/// completing with "Nothing due".
/// </remarks>
public sealed class DialerActivityReportProvider : ContactCenterReportBase, IReport
{
    private static readonly string[] _callEventTypes =
    [
        ContactCenterConstants.Events.DialerAttemptStarted,
        ContactCenterConstants.Events.DialerLiveAnswered,
        ContactCenterConstants.Events.DialerCallAbandoned,
    ];

    private static readonly DialerSuppressionReason[] _suppressionReasons =
    [
        DialerSuppressionReason.DoNotCall,
        DialerSuppressionReason.NationalDoNotCallRegistry,
        DialerSuppressionReason.OutsideCallingWindow,
        DialerSuppressionReason.NumberNotInService,
        DialerSuppressionReason.ComplianceScreeningUnavailable,
        DialerSuppressionReason.AbandonmentRateExceeded,
        DialerSuppressionReason.MaxAttemptsReached,
        DialerSuppressionReason.RetryCoolDown,
        DialerSuppressionReason.NoDestination,
    ];

    private readonly IDialerPacingHealthService _healthService;
    private readonly IDialerPacingCycleStore _cycleStore;
    private readonly IInteractionEventStore _eventStore;
    private readonly IDialerProfileReader _profileReader;
    private readonly IAgentProfileManager _agentManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerActivityReportProvider"/> class.
    /// </summary>
    /// <param name="reportingService">The Contact Center reporting service.</param>
    /// <param name="capabilityGuard">The guard that decides whether the producing capabilities are enabled.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="healthService">The service that reads what the dialer is doing for each paced campaign queue.</param>
    /// <param name="cycleStore">The record of each pacing cycle.</param>
    /// <param name="eventStore">The event log the routing decisions and suppressions are read from.</param>
    /// <param name="profileReader">The reader of the dialer profiles, for their names.</param>
    /// <param name="agentManager">The agents, for their names.</param>
    /// <param name="campaignManagers">The campaign catalog, when the campaign feature is on, for campaign names.</param>
    /// <param name="session">The YesSql session, which counts the calls from the event index without reading the events.</param>
    /// <param name="clock">The clock, so time that has not happened yet is never reported.</param>
    /// <param name="localClock">The tenant's clock, whose time zone the hours are in.</param>
    public DialerActivityReportProvider(
        IContactCenterReportingService reportingService,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<DialerActivityReportProvider> stringLocalizer,
        IDialerPacingHealthService healthService,
        IDialerPacingCycleStore cycleStore,
        IInteractionEventStore eventStore,
        IDialerProfileReader profileReader,
        IAgentProfileManager agentManager,
        IEnumerable<ICatalogManager<OmnichannelCampaign>> campaignManagers,
        ISession session,
        IClock clock,
        ILocalClock localClock)
        : base(reportingService, capabilityGuard, stringLocalizer)
    {
        _healthService = healthService;
        _cycleStore = cycleStore;
        _eventStore = eventStore;
        _profileReader = profileReader;
        _agentManager = agentManager;
        _campaignManager = campaignManagers.FirstOrDefault();
        _session = session;
        _clock = clock;
        _localClock = localClock;
    }

    /// <inheritdoc/>
    public static string Name => "contact-center-dialer-activity";

    /// <inheritdoc/>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<DialerActivityReportProvider>("Dialer activity and health");

    /// <inheritdoc/>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<DialerActivityReportProvider>("Whether the dialer is dialing, idle, holding back, has nothing to call or has stalled; its last pacing cycle and call per campaign and dialer profile; calls and routing decisions per hour; and suppressed attempts by reason.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Operations;

    /// <inheritdoc/>
    public static Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    public override IReadOnlyCollection<string> FilterNames { get; } = [];

    /// <inheritdoc/>
    /// <remarks>Pacing cycles are recorded only by the Paced Dialing feature, which also registers this report.</remarks>
    public override IReadOnlyCollection<string> RequiredFeatureIds { get; } =
    [
        ContactCenterConstants.Feature.DialerPaced,
    ];

    /// <inheritdoc/>
    protected override async Task<ReportDocument> RunCoreAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var range = context.Filter.GetDateRange();
        var toUtc = range.ToUtc is { } periodEnd && periodEnd < now
            ? periodEnd
            : now;
        var fromUtc = range.FromUtc is { } periodStart && periodStart < toUtc
            ? periodStart
            : toUtc.AddDays(-1);
        var timeZone = await ReportTimeZone.ResolveAsync(_localClock);

        var snapshot = await _healthService.EvaluateAsync(cancellationToken);
        var cycles = await _cycleStore.GetCompletedBetweenAsync(fromUtc, toUtc.AddTicks(1), cancellationToken);
        var callRows = await _session.QueryIndex<InteractionEventIndex>(
            index => index.AggregateType == nameof(DialerProfile) &&
                index.EventType.IsIn(_callEventTypes) &&
                index.OccurredUtc >= fromUtc &&
                index.OccurredUtc <= toUtc,
            collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        var callEvents = callRows.ToArray();
        var routingDecisions = (await _eventStore.GetByAggregateWindowAsync(
            nameof(QueueItem),
            [ContactCenterConstants.Events.RoutingDecisionMade],
            aggregateIds: null,
            fromUtc,
            toUtc,
            cancellationToken))
            .Select(interactionEvent => (Event: interactionEvent, Decision: DialerDecisionEvents.Read<DialerRoutingDecisionPayload>(interactionEvent)))
            .Where(entry => entry.Decision is not null && ContactCenterConstants.IsCampaignQueue(entry.Decision.QueueId))
            .ToArray();

        var suppressions = (await _eventStore.GetByAggregateWindowAsync(
            nameof(OmnichannelActivity),
            [ContactCenterConstants.Events.DialSuppressed],
            aggregateIds: null,
            fromUtc,
            toUtc,
            cancellationToken))
            .Select(interactionEvent => (Event: interactionEvent, Suppression: DialerDecisionEvents.Read<DialerSuppressionPayload>(interactionEvent)))
            .Where(entry => entry.Suppression is not null)
            .ToArray();

        var claims = (await _eventStore.GetByAggregateWindowAsync(
            nameof(DialerProfile),
            [ContactCenterConstants.Events.DialerAgentConnectClaimed],
            aggregateIds: null,
            fromUtc,
            toUtc,
            cancellationToken))
            .Select(interactionEvent => (Event: interactionEvent, Claim: DialerDecisionEvents.Read<CallLifecycleEventData>(interactionEvent)))
            .Where(entry => entry.Claim is not null && ContactCenterConstants.IsCampaignQueue(entry.Claim.QueueId))
            .ToArray();

        var names = new ReportNames(
            _agentManager,
            await LoadCampaignNamesAsync(
                snapshot.Queues.Select(queue => queue.QueueId)
                    .Concat(cycles.Select(cycle => cycle.QueueId))
                    .Concat(routingDecisions.Select(entry => entry.Decision.QueueId)),
                cancellationToken),
            S);

        var document = new ReportDocument();

        document.Add(BuildStatusSection(snapshot, timeZone, names));
        document.Add(await BuildQueueSectionAsync(snapshot, cycles, routingDecisions, claims, timeZone, names, cancellationToken));
        document.Add(await BuildProfileSectionAsync(snapshot, cycles, callEvents, suppressions, timeZone, names, cancellationToken));

        var timeline = DialerActivityTimeline.Create(fromUtc, toUtc, timeZone);

        document.Add(BuildCallsChart(timeline, callEvents));
        document.Add(BuildRoutingChart(timeline, routingDecisions));
        document.Add(BuildSuppressionSection(suppressions));

        return document;
    }

    private ReportSection BuildStatusSection(DialerPacingHealthSnapshot snapshot, ReportTimeZone timeZone, ReportNames names)
    {
        var section = ReportSection.ForMetrics(S["Dialer right now"].Value,
        [
            new ReportMetric(S["State"].Value, DescribeState(snapshot.State)),
            new ReportMetric(S["Paced campaigns with work waiting"].Value, ReportFormat.Number(snapshot.Queues.Count)),
            new ReportMetric(S["Pacing lock not acquired"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.LockNotAcquired))),
            new ReportMetric(S["Stalled"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.Stalled))),
            new ReportMetric(S["Idle: no agent available"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.Idle))),
            new ReportMetric(S["Holding back"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.HeldBack))),
            new ReportMetric(S["Dialing"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.Dialing))),
            new ReportMetric(S["Nothing due"].Value, ReportFormat.Number(Count(snapshot, DialerPacingHealthState.NothingToCall))),
            new ReportMetric(S["Stalled after"].Value, S["{0} minutes", (int)snapshot.StalledAfter.TotalMinutes].Value),
        ]);

        var lockNotAcquired = snapshot.Queues.Where(queue => queue.State == DialerPacingHealthState.LockNotAcquired).ToArray();
        var stalled = snapshot.Queues.Where(queue => queue.State == DialerPacingHealthState.Stalled).ToArray();

        if (lockNotAcquired.Length > 0)
        {
            // Named before a stall: the cycles still run, and the reason they place nothing is known.
            section.Description = S["Not being paced: the pacing lock has not been acquired since {0} ({1}), so every pacing cycle skips itself.",
                FormatTime(lockNotAcquired.Min(queue => queue.LockNotAcquiredSinceUtc) ?? snapshot.EvaluatedUtc, timeZone),
                string.Join(", ", lockNotAcquired.Select(queue => names.Campaign(queue.CampaignId, queue.QueueId)))].Value;
        }
        else if (stalled.Length > 0)
        {
            // The heartbeat warning: the one condition in this report that means pacing has stopped.
            section.Description = S["Due work is waiting but no pacing cycle has completed for {0} minutes: {1}.",
                (int)stalled.Max(queue => queue.TimeWithoutCompletedCycle).TotalMinutes,
                string.Join(", ", stalled.Select(queue => names.Campaign(queue.CampaignId, queue.QueueId)))].Value;
        }

        return section;
    }

    private async Task<ReportSection> BuildQueueSectionAsync(
        DialerPacingHealthSnapshot snapshot,
        IReadOnlyCollection<DialerPacingCycle> cycles,
        (InteractionEvent Event, DialerRoutingDecisionPayload Decision)[] routingDecisions,
        (InteractionEvent Event, CallLifecycleEventData Claim)[] claims,
        ReportTimeZone timeZone,
        ReportNames names,
        CancellationToken cancellationToken)
    {
        var states =snapshot.Queues.ToDictionary(queue => queue.QueueId, StringComparer.OrdinalIgnoreCase);
        var queueIds = snapshot.Queues
            .Select(queue => queue.QueueId)
            .Concat(cycles.Select(cycle => cycle.QueueId))
            .Concat(routingDecisions.Select(entry => entry.Decision.QueueId))
            .Where(queueId => !string.IsNullOrEmpty(queueId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<(int Severity, string Name, ReportRow Row)>();

        foreach (var queueId in queueIds)
        {
            var lastCycle = states.TryGetValue(queueId, out var health)
                ? health.LastCycle
                : await _cycleStore.FindLatestByQueueAsync(queueId, outcomes: null, cancellationToken);
            var lastPlaced = await _cycleStore.FindLatestByQueueAsync(queueId, [DialerPacingCycleOutcome.Placed], cancellationToken);
            var inPeriod = cycles.Where(cycle => string.Equals(cycle.QueueId, queueId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var decisions = routingDecisions.Where(entry => string.Equals(entry.Decision.QueueId, queueId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var name = names.Campaign(ContactCenterConstants.CampaignQueue.GetCampaignId(queueId), queueId);

            // A reserved call is assigned when routing picks its agent, an over-dialed one when an agent is claimed for the
            // person who answered; whichever happened last is the queue's last assignment.
            var lastAssigned = decisions
                .Where(entry => entry.Decision.Succeeded)
                .Select(entry => (OccurredUtc: (DateTime?)entry.Event.OccurredUtc, AgentId: entry.Decision.SelectedAgentId, ActivityItemId: entry.Decision.ActivityItemId))
                .Concat(claims
                    .Where(entry => string.Equals(entry.Claim.QueueId, queueId, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => (OccurredUtc: (DateTime?)entry.Event.OccurredUtc, entry.Claim.AgentId, entry.Claim.ActivityItemId)))
                .Append((OccurredUtc: lastPlaced?.LastAssignedAgentId is null ? null : lastPlaced?.CompletedUtc, AgentId: lastPlaced?.LastAssignedAgentId, ActivityItemId: lastPlaced?.LastAssignedActivityItemId))
                .Where(entry => entry.OccurredUtc is not null)
                .OrderByDescending(entry => entry.OccurredUtc)
                .FirstOrDefault();

            rows.Add((
                health is null ? -1 : DialerPacingHealthEvaluator.Severity(health.State),
                name,
                new ReportRow(
                [
                    name,
                    health is null ? S["No work waiting"].Value : DescribeState(health.State),
                    FormatTime(lastCycle?.StartedUtc, timeZone),
                    FormatTime(lastCycle?.CompletedUtc, timeZone),
                    DescribeOutcome(lastCycle),
                    lastCycle?.Detail ?? string.Empty,
                    await DescribeCallAsync(lastPlaced?.CompletedUtc, lastPlaced?.LastPlacedAgentId, lastPlaced?.LastPlacedActivityItemId, timeZone, names, cancellationToken),
                    await DescribeCallAsync(lastAssigned.OccurredUtc, lastAssigned.AgentId, lastAssigned.ActivityItemId, timeZone, names, cancellationToken),
                    ReportFormat.Number(inPeriod.Sum(cycle => cycle.CycleCount)),
                    ReportFormat.Number(inPeriod.Sum(cycle => cycle.CallsPlaced)),
                    ReportFormat.Number(decisions.Where(entry => entry.Decision.Succeeded).Sum(entry => entry.Decision.Occurrences)),
                    ReportFormat.Number(decisions.Where(entry => !entry.Decision.Succeeded).Sum(entry => entry.Decision.Occurrences)),
                ])));
        }

        return ReportSection.ForTable(
            S["Campaign queues"].Value,
            [
                new ReportColumn(S["Campaign"].Value),
                new ReportColumn(S["Dialer state"].Value),
                new ReportColumn(S["Last cycle started ({0})", timeZone.Name].Value),
                new ReportColumn(S["Last cycle completed ({0})", timeZone.Name].Value),
                new ReportColumn(S["Last outcome"].Value),
                new ReportColumn(S["Why"].Value),
                new ReportColumn(S["Last call placed"].Value),
                new ReportColumn(S["Last call assigned"].Value),
                new ReportColumn(S["Cycles"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Calls placed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Routed to an agent"].Value, ReportColumnAlign.End),
                new ReportColumn(S["No agent available"].Value, ReportColumnAlign.End),
            ],
            rows
                .OrderByDescending(entry => entry.Severity)
                .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => entry.Row));
    }

    private async Task<ReportSection> BuildProfileSectionAsync(
        DialerPacingHealthSnapshot snapshot,
        IReadOnlyCollection<DialerPacingCycle> cycles,
        InteractionEventIndex[] callEvents,
        (InteractionEvent Event, DialerSuppressionPayload Suppression)[] suppressions,
        ReportTimeZone timeZone,
        ReportNames names,
        CancellationToken cancellationToken)
    {
        var profileIds = snapshot.Queues
            .Select(queue => queue.DialerProfileId)
            .Concat(cycles.Select(cycle => cycle.DialerProfileId))
            .Concat(callEvents.Select(row => row.AggregateId))
            .Concat(suppressions.Select(entry => entry.Suppression.ProfileItemId))
            .Where(profileId => !string.IsNullOrEmpty(profileId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var rows = new List<(string Name, ReportRow Row)>();

        foreach (var profileId in profileIds)
        {
            var profile = await _profileReader.FindByIdAsync(profileId, cancellationToken);
            var lastCycle = await _cycleStore.FindLatestByProfileAsync(profileId, outcomes: null, cancellationToken);
            var lastPlaced = await _cycleStore.FindLatestByProfileAsync(profileId, [DialerPacingCycleOutcome.Placed], cancellationToken);
            var events = callEvents.Where(row => string.Equals(row.AggregateId, profileId, StringComparison.Ordinal)).ToArray();
            var name = string.IsNullOrWhiteSpace(profile?.Name) ? S["(Unknown dialer profile)"].Value : profile.Name;

            rows.Add((name, new ReportRow(
            [
                name,
                profile is null ? string.Empty : profile.Mode.ToString(),
                FormatTime(lastCycle?.StartedUtc, timeZone),
                FormatTime(lastCycle?.CompletedUtc, timeZone),
                DescribeOutcome(lastCycle),
                await DescribeCallAsync(lastPlaced?.CompletedUtc, lastPlaced?.LastPlacedAgentId, lastPlaced?.LastPlacedActivityItemId, timeZone, names, cancellationToken),
                ReportFormat.Number(events.Count(row => row.EventType == ContactCenterConstants.Events.DialerAttemptStarted)),
                ReportFormat.Number(events.Count(row => row.EventType == ContactCenterConstants.Events.DialerLiveAnswered)),
                ReportFormat.Number(events.Count(row => row.EventType == ContactCenterConstants.Events.DialerCallAbandoned)),
                ReportFormat.Number(suppressions
                    .Where(entry => string.Equals(entry.Suppression.ProfileItemId, profileId, StringComparison.Ordinal))
                    .Sum(entry => entry.Suppression.Occurrences)),
            ])));
        }

        return ReportSection.ForTable(
            S["Dialer profiles"].Value,
            [
                new ReportColumn(S["Dialer profile"].Value),
                new ReportColumn(S["Mode"].Value),
                new ReportColumn(S["Last cycle started ({0})", timeZone.Name].Value),
                new ReportColumn(S["Last cycle completed ({0})", timeZone.Name].Value),
                new ReportColumn(S["Last outcome"].Value),
                new ReportColumn(S["Last call placed"].Value),
                new ReportColumn(S["Calls placed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Suppressed"].Value, ReportColumnAlign.End),
            ],
            rows
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(entry => entry.Row));
    }

    private ReportSection BuildCallsChart(DialerActivityTimeline timeline, InteractionEventIndex[] callEvents)
    {
        double[] Series(string eventType)
            => timeline.Count(callEvents
                .Where(row => row.EventType == eventType)
                .Select(row => (row.OccurredUtc, 1)));

        return ReportSection.ForChart(
            timeline.Hourly ? S["Calls per hour"].Value : S["Calls per day"].Value,
            new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = [.. timeline.Labels],
                Datasets =
                [
                    new ReportChartDataset(S["Placed"].Value, Series(ContactCenterConstants.Events.DialerAttemptStarted)),
                    new ReportChartDataset(S["Answered by a person"].Value, Series(ContactCenterConstants.Events.DialerLiveAnswered)),
                    new ReportChartDataset(S["Abandoned"].Value, Series(ContactCenterConstants.Events.DialerCallAbandoned)),
                ],
            },
            width: 12);
    }

    private ReportSection BuildRoutingChart(
        DialerActivityTimeline timeline,
        (InteractionEvent Event, DialerRoutingDecisionPayload Decision)[] routingDecisions)
    {
        double[] Series(bool succeeded)
            => timeline.Count(routingDecisions
                .Where(entry => entry.Decision.Succeeded == succeeded)
                .Select(entry => (entry.Event.OccurredUtc, entry.Decision.Occurrences)));

        return ReportSection.ForChart(
            timeline.Hourly ? S["Campaign routing decisions per hour"].Value : S["Campaign routing decisions per day"].Value,
            new ReportChart
            {
                Type = ReportChartType.Bar,
                Stacked = true,
                Labels = [.. timeline.Labels],
                Datasets =
                [
                    new ReportChartDataset(S["Routed to an agent"].Value, Series(succeeded: true)),
                    new ReportChartDataset(S["No agent available"].Value, Series(succeeded: false)),
                ],
            },
            width: 12);
    }

    private ReportSection BuildSuppressionSection((InteractionEvent Event, DialerSuppressionPayload Suppression)[] suppressions)
    {
        var counts = suppressions
            .GroupBy(entry => entry.Suppression.Reason)
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Suppression.Occurrences));

        var rows = _suppressionReasons
            .Select(reason => new ReportRow(
            [
                DescribeSuppression(reason),
                ReportFormat.Number(counts.GetValueOrDefault(reason)),
            ]))
            .ToList();

        rows.Add(new ReportRow(
            [
                S["All reasons"].Value,
                ReportFormat.Number(counts.Values.Sum()),
            ],
            ReportRowKind.GrandTotal));

        return ReportSection.ForTable(
            S["Suppressed attempts by reason"].Value,
            [
                new ReportColumn(S["Reason"].Value),
                new ReportColumn(S["Attempts"].Value, ReportColumnAlign.End),
            ],
            rows);
    }

    private async Task<string> DescribeCallAsync(
        DateTime? occurredUtc,
        string agentId,
        string activityItemId,
        ReportTimeZone timeZone,
        ReportNames names,
        CancellationToken cancellationToken)
    {
        if (occurredUtc is null)
        {
            return S["None"].Value;
        }

        var agent = string.IsNullOrEmpty(agentId)
            ? S["no agent yet"].Value
            : await names.AgentAsync(agentId, cancellationToken);

        return S["{0} · {1} · record {2}", FormatTime(occurredUtc, timeZone), agent, activityItemId ?? "?"].Value;
    }

    internal string DescribeOutcome(DialerPacingCycle cycle)
    {
        if (cycle is null)
        {
            return S["No cycle recorded"].Value;
        }

        var outcome = cycle.Outcome switch
        {
            DialerPacingCycleOutcome.Placed => S["Placed {0}", cycle.CallsPlaced].Value,
            DialerPacingCycleOutcome.NothingDue => S["Nothing due"].Value,
            DialerPacingCycleOutcome.NoAgents => S["No agents"].Value,
            DialerPacingCycleOutcome.HeldBack => S["Held back"].Value,
            DialerPacingCycleOutcome.LockNotAcquired => S["Lock not acquired"].Value,
            DialerPacingCycleOutcome.TimeBudgetExceeded => S["Time budget exceeded"].Value,
            DialerPacingCycleOutcome.WriteConflict => S["Lost a write race"].Value,
            _ => S["Failed"].Value,
        };

        return cycle.CycleCount > 1
            ? S["{0} ({1} cycles in a row)", outcome, cycle.CycleCount].Value
            : outcome;
    }

    internal string DescribeState(DialerPacingHealthState state)
        => state switch
        {
            DialerPacingHealthState.LockNotAcquired => S["Not pacing: pacing lock not acquired"].Value,
            DialerPacingHealthState.Stalled => S["Stalled"].Value,
            DialerPacingHealthState.Idle => S["Idle: no agent available"].Value,
            DialerPacingHealthState.HeldBack => S["Holding back"].Value,
            DialerPacingHealthState.Dialing => S["Dialing"].Value,
            DialerPacingHealthState.Starting => S["Starting"].Value,
            _ => S["Nothing to call"].Value,
        };

    private string DescribeSuppression(DialerSuppressionReason reason)
        => reason switch
        {
            DialerSuppressionReason.DoNotCall => S["Do not call"].Value,
            DialerSuppressionReason.NationalDoNotCallRegistry => S["On the national do-not-call registry"].Value,
            DialerSuppressionReason.OutsideCallingWindow => S["Outside the calling window"].Value,
            DialerSuppressionReason.NumberNotInService => S["Number not in service"].Value,
            DialerSuppressionReason.ComplianceScreeningUnavailable => S["Compliance check unavailable"].Value,
            DialerSuppressionReason.AbandonmentRateExceeded => S["Abandonment rate cap reached"].Value,
            DialerSuppressionReason.MaxAttemptsReached => S["Out of attempts"].Value,
            DialerSuppressionReason.RetryCoolDown => S["Waiting out the retry delay"].Value,
            DialerSuppressionReason.NoDestination => S["No number to dial"].Value,
            _ => reason.ToString(),
        };

    private static string FormatTime(DateTime? utc, ReportTimeZone timeZone)
        => utc is { } value
            ? timeZone.ToLocal(value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "—";

    private static int Count(DialerPacingHealthSnapshot snapshot, DialerPacingHealthState state)
        => snapshot.Queues.Count(queue => queue.State == state);

    private async Task<IReadOnlyDictionary<string, string>> LoadCampaignNamesAsync(IEnumerable<string> queueIds, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        if (_campaignManager is null)
        {
            return names;
        }

        var campaignIds = queueIds
            .Where(queueId => !string.IsNullOrEmpty(queueId))
            .Select(ContactCenterConstants.CampaignQueue.GetCampaignId)
            .Where(campaignId => !string.IsNullOrEmpty(campaignId))
            .Distinct(StringComparer.Ordinal);

        foreach (var campaignId in campaignIds)
        {
            var campaign = await _campaignManager.FindByIdAsync(campaignId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(campaign?.DisplayText))
            {
                names[campaignId] = campaign.DisplayText;
            }
        }

        return names;
    }

    /// <summary>
    /// Names the campaigns and agents the report mentions, reading each agent once.
    /// </summary>
    private sealed class ReportNames
    {
        private readonly IAgentProfileManager _agentManager;
        private readonly IReadOnlyDictionary<string, string> _campaigns;
        private readonly IStringLocalizer S;
        private readonly Dictionary<string, string> _agents = new(StringComparer.Ordinal);

        public ReportNames(
            IAgentProfileManager agentManager,
            IReadOnlyDictionary<string, string> campaigns,
            IStringLocalizer stringLocalizer)
        {
            _agentManager = agentManager;
            _campaigns = campaigns;
            S = stringLocalizer;
        }

        public string Campaign(string campaignId, string queueId)
        {
            if (string.IsNullOrEmpty(campaignId))
            {
                return queueId;
            }

            return _campaigns.TryGetValue(campaignId, out var name)
                ? name
                : campaignId;
        }

        public async Task<string> AgentAsync(string agentId, CancellationToken cancellationToken)
        {
            if (_agents.TryGetValue(agentId, out var name))
            {
                return name;
            }

            var agent = await _agentManager.FindByIdAsync(agentId, cancellationToken);

            if (agent is null)
            {
                name = S["(Unknown agent)"].Value;
            }
            else if (!string.IsNullOrWhiteSpace(agent.DisplayName))
            {
                name = agent.DisplayName;
            }
            else
            {
                name = ReportValue.UserDisplayName(agent.UserName, S["(Unknown agent)"].Value);
            }

            _agents[agentId] = name;

            return name;
        }
    }
}
