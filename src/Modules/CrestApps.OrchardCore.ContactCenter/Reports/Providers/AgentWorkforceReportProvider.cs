using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

internal sealed class AgentWorkforceReportProvider : IReport, IReportFilterMetadata
{
    private readonly ISession _session;
    private readonly IAgentProfileManager _agentManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly AgentWorkforceReportDefinition _definition;
    private readonly IStringLocalizer _stringLocalizer;

    public AgentWorkforceReportProvider(
        ISession session,
        IAgentProfileManager agentManager,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        AgentWorkforceReportDefinition definition,
        IStringLocalizer stringLocalizer)
    {
        _session = session;
        _agentManager = agentManager;
        _campaignManager = campaignManager;
        _definition = definition;
        _stringLocalizer = stringLocalizer;
    }

    public string Name => _definition.Name;

    public LocalizedString DisplayName => _definition.DisplayName();

    public LocalizedString Description => _definition.Description();

    public string Category => _definition.Category;

    public Permission Permission => ContactCenterPermissions.ViewReports;

    public IReadOnlyCollection<string> FilterNames => _definition.FilterNames;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var events = (await _session.Query<InteractionEvent, InteractionEventIndex>(
            index =>
                index.AggregateType == nameof(AgentProfile) &&
                index.OccurredUtc <= context.ToUtc &&
                (index.EventType == ContactCenterConstants.Events.AgentSignedIn ||
                 index.EventType == ContactCenterConstants.Events.AgentSignedOut ||
                 index.EventType == ContactCenterConstants.Events.AgentPresenceChanged),
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken))
            .ToArray();
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);

        if (!string.IsNullOrEmpty(criteria.AgentId))
        {
            events = events
                .Where(interactionEvent => string.Equals(interactionEvent.AggregateId, criteria.AgentId, StringComparison.Ordinal))
                .ToArray();
        }

        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .ToDictionary(agent => agent.ItemId, StringComparer.Ordinal);
        var intervals = BuildIntervals(events, context.FromUtc, context.ToUtc);
        var campaignNames = (await _campaignManager.GetAllAsync(cancellationToken))
            .Where(campaign => !string.IsNullOrEmpty(campaign.ItemId))
            .ToDictionary(
                campaign => campaign.ItemId,
                campaign => campaign.DisplayText ?? campaign.ItemId,
                StringComparer.Ordinal);

        return _definition.Kind switch
        {
            AgentWorkforceReportKind.TimeSummary => BuildTimeSummary(intervals, agents),
            AgentWorkforceReportKind.DailyTimecard => BuildDailyTimecard(intervals, agents),
            AgentWorkforceReportKind.StatusDuration => BuildStatusDuration(intervals),
            AgentWorkforceReportKind.BreakAnalysis => BuildBreakAnalysis(intervals, agents),
            AgentWorkforceReportKind.ReadyNotReady => BuildReadyNotReady(intervals, agents),
            AgentWorkforceReportKind.Utilization => BuildUtilization(intervals, agents, occupancy: false),
            AgentWorkforceReportKind.Occupancy => BuildUtilization(intervals, agents, occupancy: true),
            AgentWorkforceReportKind.ReasonBreakdown => BuildReasonBreakdown(intervals),
            AgentWorkforceReportKind.PresenceAudit => BuildPresenceAudit(events, agents, context.FromUtc, context.ToUtc),
            AgentWorkforceReportKind.QueueMembershipHours => BuildMembershipHours(intervals, queueMembership: true),
            AgentWorkforceReportKind.CampaignMembershipHours => BuildMembershipHours(intervals, queueMembership: false, campaignNames),
            AgentWorkforceReportKind.PayrollTimecard => BuildPayrollTimecard(intervals, agents),
            _ => new ReportDocument(),
        };
    }

    private IStringLocalizer S => _stringLocalizer;

    internal static IReadOnlyList<AgentPresenceInterval> BuildIntervals(
        IEnumerable<InteractionEvent> events,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var intervals = new List<AgentPresenceInterval>();

        foreach (var agentEvents in events
            .Where(interactionEvent => interactionEvent.OccurredUtc <= toUtc)
            .GroupBy(interactionEvent => interactionEvent.AggregateId, StringComparer.Ordinal))
        {
            var transitions = agentEvents
                .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
                .Select(interactionEvent => new
                {
                    Event = interactionEvent,
                    Data = interactionEvent.GetData<AgentPresenceChangedEventData>(),
                })
                .Where(entry => entry.Data is not null)
                .ToArray();

            for (var index = 0; index < transitions.Length; index++)
            {
                var transition = transitions[index];
                var startUtc = GetChangedUtc(transition.Event, transition.Data);
                var endUtc = index + 1 < transitions.Length
                    ? GetChangedUtc(transitions[index + 1].Event, transitions[index + 1].Data)
                    : toUtc;
                var clippedStart = startUtc < fromUtc ? fromUtc : startUtc;
                var clippedEnd = endUtc > toUtc ? toUtc : endUtc;

                if (clippedEnd <= clippedStart || endUtc <= fromUtc || startUtc > toUtc)
                {
                    continue;
                }

                intervals.Add(new AgentPresenceInterval
                {
                    AgentId = agentEvents.Key,
                    Status = transition.Data.CurrentStatus,
                    Reason = transition.Data.Reason,
                    QueueIds = [.. transition.Data.QueueIds],
                    CampaignIds = [.. transition.Data.CampaignIds],
                    StartUtc = clippedStart,
                    EndUtc = clippedEnd,
                });
            }
        }

        return intervals;
    }

    private static DateTime GetChangedUtc(InteractionEvent interactionEvent, AgentPresenceChangedEventData data)
    {
        return data.ChangedUtc == default ? interactionEvent.OccurredUtc : data.ChangedUtc;
    }

    private ReportDocument BuildTimeSummary(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Signed-in time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Available"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Busy"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Wrap-up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Break"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Other not ready"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Utilization"].Value, ReportColumnAlign.End),
        };
        var rows = intervals
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var summary = AgentTimeSummary.Create(group);

                return new
                {
                    summary.SignedInSeconds,
                    Row = new ReportRow(
                    [
                        ResolveAgentName(group.Key, agents),
                        ReportFormat.Duration(summary.SignedInSeconds),
                        ReportFormat.Duration(summary.AvailableSeconds),
                        ReportFormat.Duration(summary.BusySeconds),
                        ReportFormat.Duration(summary.WrapUpSeconds),
                        ReportFormat.Duration(summary.BreakSeconds),
                        ReportFormat.Duration(summary.OtherNotReadySeconds),
                        ReportFormat.Percent(summary.Utilization),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.SignedInSeconds)
            .Select(entry => entry.Row)
            .ToList();

        var totals = AgentTimeSummary.Create(intervals);
        rows.Add(new ReportRow(
        [
            S["All agents"].Value,
            ReportFormat.Duration(totals.SignedInSeconds),
            ReportFormat.Duration(totals.AvailableSeconds),
            ReportFormat.Duration(totals.BusySeconds),
            ReportFormat.Duration(totals.WrapUpSeconds),
            ReportFormat.Duration(totals.BreakSeconds),
            ReportFormat.Duration(totals.OtherNotReadySeconds),
            ReportFormat.Percent(totals.Utilization),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agent time summary"].Value, columns, rows));
    }

    private ReportDocument BuildDailyTimecard(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var daily = SplitByUtcDay(intervals);
        var columns = new[]
        {
            new ReportColumn(S["Date (UTC)"].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Signed-in time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Productive presence"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Busy + wrap-up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Break + away"].Value, ReportColumnAlign.End),
            new ReportColumn(S["First observed"].Value),
            new ReportColumn(S["Last observed"].Value),
        };
        var rows = new List<ReportRow>();

        foreach (var dayGroup in daily
            .GroupBy(interval => DateOnly.FromDateTime(interval.StartUtc))
            .OrderBy(group => group.Key))
        {
            foreach (var agentGroup in dayGroup
                .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
                .OrderBy(group => ResolveAgentName(group.Key, agents)))
            {
                var summary = AgentTimeSummary.Create(agentGroup);

                rows.Add(new ReportRow(
                [
                    dayGroup.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ResolveAgentName(agentGroup.Key, agents),
                    ReportFormat.Duration(summary.SignedInSeconds),
                    ReportFormat.Duration(summary.ProductivePresenceSeconds),
                    ReportFormat.Duration(summary.WorkSeconds),
                    ReportFormat.Duration(summary.BreakAndAwaySeconds),
                    agentGroup.Min(interval => interval.StartUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    agentGroup.Max(interval => interval.EndUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                ]));
            }

            var dayTotals = AgentTimeSummary.Create(dayGroup);
            rows.Add(new ReportRow(
            [
                dayGroup.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                S["Day subtotal"].Value,
                ReportFormat.Duration(dayTotals.SignedInSeconds),
                ReportFormat.Duration(dayTotals.ProductivePresenceSeconds),
                ReportFormat.Duration(dayTotals.WorkSeconds),
                ReportFormat.Duration(dayTotals.BreakAndAwaySeconds),
                dayGroup.Min(interval => interval.StartUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                dayGroup.Max(interval => interval.EndUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            ], ReportRowKind.Subtotal));
        }

        var totals = AgentTimeSummary.Create(daily);
        rows.Add(new ReportRow(
        [
            S["All dates"].Value,
            S["All agents"].Value,
            ReportFormat.Duration(totals.SignedInSeconds),
            ReportFormat.Duration(totals.ProductivePresenceSeconds),
            ReportFormat.Duration(totals.WorkSeconds),
            ReportFormat.Duration(totals.BreakAndAwaySeconds),
            "—",
            "—",
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Daily agent timecard"].Value, columns, rows));
    }

    private ReportDocument BuildStatusDuration(IReadOnlyList<AgentPresenceInterval> intervals)
    {
        var total = intervals.Where(interval => interval.Status != AgentPresenceStatus.Offline).Sum(interval => interval.DurationSeconds);
        var columns = new[]
        {
            new ReportColumn(S["Presence status"].Value),
            new ReportColumn(S["Duration"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Share of signed-in time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Intervals"].Value, ReportColumnAlign.End),
        };
        var population = intervals.Where(interval => interval.Status != AgentPresenceStatus.Offline).ToArray();
        var rows = population
            .GroupBy(interval => interval.Status)
            .Select(group =>
            {
                var duration = group.Sum(interval => interval.DurationSeconds);

                return new
                {
                    Duration = duration,
                    Row = new ReportRow(
                    [
                        group.Key.ToString(),
                        ReportFormat.Duration(duration),
                        ReportFormat.Percent(total > 0d ? duration / total : 0d),
                        ReportFormat.Number(group.LongCount()),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Duration)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Duration(total),
            ReportFormat.Percent(total > 0d ? 1d : 0d),
            ReportFormat.Number(population.LongLength),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Presence status duration"].Value, columns, rows));
    }

    private ReportDocument BuildBreakAnalysis(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var breaks = intervals
            .Where(interval => interval.Status is AgentPresenceStatus.Break or AgentPresenceStatus.Away)
            .ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Breaks"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Total break time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average break"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Longest break"].Value, ReportColumnAlign.End),
        };
        var rows = breaks
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var durations = group.Select(interval => interval.DurationSeconds).ToArray();

                return new
                {
                    Total = durations.Sum(),
                    Row = new ReportRow(
                    [
                        ResolveAgentName(group.Key, agents),
                        ReportFormat.Number(durations.LongLength),
                        ReportFormat.Duration(durations.Sum()),
                        ReportFormat.Duration(durations.Length > 0 ? durations.Average() : 0d),
                        ReportFormat.Duration(durations.Length > 0 ? durations.Max() : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Total)
            .Select(entry => entry.Row)
            .ToList();

        var breakSeconds = breaks.Sum(interval => interval.DurationSeconds);
        rows.Add(new ReportRow(
        [
            S["All agents"].Value,
            ReportFormat.Number(breaks.LongLength),
            ReportFormat.Duration(breakSeconds),
            ReportFormat.Duration(breaks.Length > 0 ? breakSeconds / breaks.Length : 0d),
            ReportFormat.Duration(breaks.Length > 0 ? breaks.Max(interval => interval.DurationSeconds) : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agent break and away analysis"].Value, columns, rows));
    }

    private ReportDocument BuildReadyNotReady(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Ready time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Working time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Not-ready time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Ready share"].Value, ReportColumnAlign.End),
        };
        var rows = intervals
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var summary = AgentTimeSummary.Create(group);
                var readySeconds = summary.AvailableSeconds + summary.ReservedSeconds;
                var notReadySeconds = Math.Max(0d, summary.SignedInSeconds - readySeconds - summary.WorkSeconds);

                return new ReportRow(
                [
                    ResolveAgentName(group.Key, agents),
                    ReportFormat.Duration(readySeconds),
                    ReportFormat.Duration(summary.WorkSeconds),
                    ReportFormat.Duration(notReadySeconds),
                    ReportFormat.Percent(summary.SignedInSeconds > 0d ? readySeconds / summary.SignedInSeconds : 0d),
                ]);
            })
            .ToList();

        var totals = AgentTimeSummary.Create(intervals);
        var readySeconds = totals.AvailableSeconds + totals.ReservedSeconds;
        var notReadySeconds = Math.Max(0d, totals.SignedInSeconds - readySeconds - totals.WorkSeconds);
        rows.Add(new ReportRow(
        [
            S["All agents"].Value,
            ReportFormat.Duration(readySeconds),
            ReportFormat.Duration(totals.WorkSeconds),
            ReportFormat.Duration(notReadySeconds),
            ReportFormat.Percent(totals.SignedInSeconds > 0d ? readySeconds / totals.SignedInSeconds : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Ready versus not-ready time"].Value, columns, rows));
    }

    private ReportDocument BuildUtilization(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents,
        bool occupancy)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Working time"].Value, ReportColumnAlign.End),
            new ReportColumn(occupancy ? S["Available handling time"].Value : S["Signed-in time"].Value, ReportColumnAlign.End),
            new ReportColumn(occupancy ? S["Occupancy"].Value : S["Utilization"].Value, ReportColumnAlign.End),
        };
        var rows = intervals
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var summary = AgentTimeSummary.Create(group);
                var denominator = occupancy
                    ? summary.AvailableSeconds + summary.ReservedSeconds + summary.WorkSeconds
                    : summary.SignedInSeconds;
                var ratio = denominator > 0d ? summary.WorkSeconds / denominator : 0d;

                return new
                {
                    Ratio = ratio,
                    Row = new ReportRow(
                    [
                        ResolveAgentName(group.Key, agents),
                        ReportFormat.Duration(summary.WorkSeconds),
                        ReportFormat.Duration(denominator),
                        ReportFormat.Percent(ratio),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Ratio)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(CreateUtilizationGrandTotalRow(
            intervals,
            S["All agents"].Value,
            occupancy));

        return new ReportDocument()
            .Add(ReportSection.ForTable(occupancy ? S["Agent occupancy"].Value : S["Agent utilization"].Value, columns, rows));
    }

    internal static ReportRow CreateUtilizationGrandTotalRow(
        IEnumerable<AgentPresenceInterval> intervals,
        string label,
        bool occupancy)
    {
        var summary = AgentTimeSummary.Create(intervals);
        var denominator = occupancy
            ? summary.AvailableSeconds + summary.ReservedSeconds + summary.WorkSeconds
            : summary.SignedInSeconds;

        return new ReportRow(
        [
            label,
            ReportFormat.Duration(summary.WorkSeconds),
            ReportFormat.Duration(denominator),
            ReportFormat.Percent(denominator > 0d ? summary.WorkSeconds / denominator : 0d),
        ], ReportRowKind.GrandTotal);
    }

    private ReportDocument BuildReasonBreakdown(IReadOnlyList<AgentPresenceInterval> intervals)
    {
        var columns = new[]
        {
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Reason"].Value),
            new ReportColumn(S["Duration"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Intervals"].Value, ReportColumnAlign.End),
        };
        var population = intervals.Where(interval => !string.IsNullOrWhiteSpace(interval.Reason)).ToArray();
        var rows = population
            .GroupBy(interval => new { interval.Status, interval.Reason })
            .Select(group =>
            {
                var duration = group.Sum(interval => interval.DurationSeconds);

                return new
                {
                    Duration = duration,
                    Row = new ReportRow(
                    [
                        group.Key.Status.ToString(),
                        group.Key.Reason,
                        ReportFormat.Duration(duration),
                        ReportFormat.Number(group.LongCount()),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Duration)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            "—",
            ReportFormat.Duration(population.Sum(interval => interval.DurationSeconds)),
            ReportFormat.Number(population.LongLength),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Presence reason breakdown"].Value, columns, rows));
    }

    private ReportDocument BuildPresenceAudit(
        IReadOnlyList<InteractionEvent> events,
        Dictionary<string, AgentProfile> agents,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var columns = new[]
        {
            new ReportColumn(S["Changed (UTC)"].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Previous"].Value),
            new ReportColumn(S["Current"].Value),
            new ReportColumn(S["Requested"].Value),
            new ReportColumn(S["Reason"].Value),
            new ReportColumn(S["Queues"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Campaigns"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Event"].Value),
        };
        var rows = events
            .Where(interactionEvent => interactionEvent.OccurredUtc >= fromUtc && interactionEvent.OccurredUtc <= toUtc)
            .OrderByDescending(interactionEvent => interactionEvent.OccurredUtc)
            .Select(interactionEvent => new
            {
                Event = interactionEvent,
                Data = interactionEvent.GetData<AgentPresenceChangedEventData>(),
            })
            .Where(entry => entry.Data is not null)
            .Select(entry => new ReportRow(
            [
                entry.Event.OccurredUtc.ToString("u", CultureInfo.InvariantCulture),
                ResolveAgentName(entry.Event.AggregateId, agents),
                entry.Data.PreviousStatus.ToString(),
                entry.Data.CurrentStatus.ToString(),
                entry.Data.RequestedStatus?.ToString() ?? "—",
                entry.Data.Reason ?? "—",
                ReportFormat.Number(entry.Data.QueueIds.Count),
                ReportFormat.Number(entry.Data.CampaignIds.Count),
                entry.Event.EventType,
            ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agent presence audit"].Value, columns, rows));
    }

    private ReportDocument BuildMembershipHours(
        IReadOnlyList<AgentPresenceInterval> intervals,
        bool queueMembership,
        IReadOnlyDictionary<string, string> campaignNames = null)
    {
        var memberships = intervals
            .Where(interval => interval.Status != AgentPresenceStatus.Offline)
            .SelectMany(interval => (queueMembership ? interval.QueueIds : interval.CampaignIds)
                .Select(id => new { Id = id, interval.DurationSeconds }))
            .ToArray();
        var columns = new[]
        {
            new ReportColumn(queueMembership ? S["Queue"].Value : S["Campaign"].Value),
            new ReportColumn(S["Signed-in time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Agent intervals"].Value, ReportColumnAlign.End),
        };
        var rows = memberships
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Select(group =>
            {
                var duration = group.Sum(entry => entry.DurationSeconds);

                return new
                {
                    Duration = duration,
                    Row = new ReportRow(
                    [
                        queueMembership
                            ? group.Key
                            : campaignNames?.GetValueOrDefault(group.Key) ?? group.Key,
                        ReportFormat.Duration(duration),
                        ReportFormat.Number(group.LongCount()),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Duration)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Duration(memberships.Sum(entry => entry.DurationSeconds)),
            ReportFormat.Number(memberships.LongLength),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(
                queueMembership ? S["Queue signed-in hours"].Value : S["Campaign signed-in hours"].Value,
                columns,
                rows));
    }

    private ReportDocument BuildPayrollTimecard(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Observed on-duty time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Productive presence"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Break + away"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Meeting + training"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Other not ready"].Value, ReportColumnAlign.End),
        };
        var rows = intervals
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var summary = AgentTimeSummary.Create(group);

                return new ReportRow(
                [
                    ResolveAgentName(group.Key, agents),
                    ReportFormat.Duration(summary.SignedInSeconds),
                    ReportFormat.Duration(summary.ProductivePresenceSeconds),
                    ReportFormat.Duration(summary.BreakAndAwaySeconds),
                    ReportFormat.Duration(summary.MeetingAndTrainingSeconds),
                    ReportFormat.Duration(summary.OtherNotReadySeconds),
                ]);
            })
            .ToList();

        var totals = AgentTimeSummary.Create(intervals);
        rows.Add(new ReportRow(
        [
            S["All agents"].Value,
            ReportFormat.Duration(totals.SignedInSeconds),
            ReportFormat.Duration(totals.ProductivePresenceSeconds),
            ReportFormat.Duration(totals.BreakAndAwaySeconds),
            ReportFormat.Duration(totals.MeetingAndTrainingSeconds),
            ReportFormat.Duration(totals.OtherNotReadySeconds),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Payroll timecard inputs"].Value, columns, rows));
    }

    private static List<AgentPresenceInterval> SplitByUtcDay(IEnumerable<AgentPresenceInterval> intervals)
    {
        var result = new List<AgentPresenceInterval>();

        foreach (var interval in intervals)
        {
            var start = interval.StartUtc;

            while (start < interval.EndUtc)
            {
                var nextDay = start.Date.AddDays(1);
                var end = interval.EndUtc < nextDay ? interval.EndUtc : nextDay;

                result.Add(new AgentPresenceInterval
                {
                    AgentId = interval.AgentId,
                    Status = interval.Status,
                    Reason = interval.Reason,
                    QueueIds = [.. interval.QueueIds],
                    CampaignIds = [.. interval.CampaignIds],
                    StartUtc = start,
                    EndUtc = end,
                });

                start = end;
            }
        }

        return result;
    }

    private static string ResolveAgentName(string agentId, Dictionary<string, AgentProfile> agents)
    {
        if (!string.IsNullOrEmpty(agentId) && agents.TryGetValue(agentId, out var agent))
        {
            return ReportValue.UserDisplayName(agent.UserName, "(Unknown agent)");
        }

        return "(Unknown agent)";
    }

    private sealed class AgentTimeSummary
    {
        public double SignedInSeconds { get; private set; }

        public double AvailableSeconds { get; private set; }

        public double ReservedSeconds { get; private set; }

        public double BusySeconds { get; private set; }

        public double WrapUpSeconds { get; private set; }

        public double BreakSeconds { get; private set; }

        public double AwaySeconds { get; private set; }

        public double MeetingSeconds { get; private set; }

        public double TrainingSeconds { get; private set; }

        public double OtherNotReadySeconds { get; private set; }

        public double WorkSeconds => BusySeconds + WrapUpSeconds;

        public double ProductivePresenceSeconds => AvailableSeconds + ReservedSeconds + WorkSeconds;

        public double BreakAndAwaySeconds => BreakSeconds + AwaySeconds;

        public double MeetingAndTrainingSeconds => MeetingSeconds + TrainingSeconds;

        public double Utilization => SignedInSeconds > 0d ? WorkSeconds / SignedInSeconds : 0d;

        public static AgentTimeSummary Create(IEnumerable<AgentPresenceInterval> intervals)
        {
            var result = new AgentTimeSummary();

            foreach (var interval in intervals)
            {
                var duration = interval.DurationSeconds;

                if (interval.Status != AgentPresenceStatus.Offline)
                {
                    result.SignedInSeconds += duration;
                }

                switch (interval.Status)
                {
                    case AgentPresenceStatus.Available:
                        result.AvailableSeconds += duration;
                        break;
                    case AgentPresenceStatus.Reserved:
                        result.ReservedSeconds += duration;
                        break;
                    case AgentPresenceStatus.Busy:
                        result.BusySeconds += duration;
                        break;
                    case AgentPresenceStatus.WrapUp:
                        result.WrapUpSeconds += duration;
                        break;
                    case AgentPresenceStatus.Break:
                        result.BreakSeconds += duration;
                        break;
                    case AgentPresenceStatus.Away:
                        result.AwaySeconds += duration;
                        break;
                    case AgentPresenceStatus.Meeting:
                        result.MeetingSeconds += duration;
                        break;
                    case AgentPresenceStatus.Training:
                        result.TrainingSeconds += duration;
                        break;
                    case AgentPresenceStatus.Offline:
                        break;
                    default:
                        result.OtherNotReadySeconds += duration;
                        break;
                }
            }

            return result;
        }
    }
}
