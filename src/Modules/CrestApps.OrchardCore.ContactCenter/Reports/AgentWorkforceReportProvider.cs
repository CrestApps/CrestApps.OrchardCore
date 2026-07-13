using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Reports;

internal sealed class AgentWorkforceReportProvider : IReport
{
    private readonly ISession _session;
    private readonly IAgentProfileManager _agentManager;
    private readonly AgentWorkforceReportDefinition _definition;
    private readonly IStringLocalizer _stringLocalizer;

    public AgentWorkforceReportProvider(
        ISession session,
        IAgentProfileManager agentManager,
        AgentWorkforceReportDefinition definition,
        IStringLocalizer stringLocalizer)
    {
        _session = session;
        _agentManager = agentManager;
        _definition = definition;
        _stringLocalizer = stringLocalizer;
    }

    public string Name => _definition.Name;

    public LocalizedString DisplayName => _definition.DisplayName();

    public LocalizedString Description => _definition.Description();

    public string Category => _definition.Category;

    public Permission Permission => ContactCenterPermissions.ViewReports;

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
            AgentWorkforceReportKind.CampaignMembershipHours => BuildMembershipHours(intervals, queueMembership: false),
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
            .Select(entry => entry.Row);

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
        var rows = daily
            .GroupBy(interval => new { Date = DateOnly.FromDateTime(interval.StartUtc), interval.AgentId })
            .OrderBy(group => group.Key.Date)
            .ThenBy(group => ResolveAgentName(group.Key.AgentId, agents))
            .Select(group =>
            {
                var summary = AgentTimeSummary.Create(group);

                return new ReportRow(
                [
                    group.Key.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ResolveAgentName(group.Key.AgentId, agents),
                    ReportFormat.Duration(summary.SignedInSeconds),
                    ReportFormat.Duration(summary.ProductivePresenceSeconds),
                    ReportFormat.Duration(summary.WorkSeconds),
                    ReportFormat.Duration(summary.BreakAndAwaySeconds),
                    group.Min(interval => interval.StartUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    group.Max(interval => interval.EndUtc).ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                ]);
            });

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
        var rows = intervals
            .Where(interval => interval.Status != AgentPresenceStatus.Offline)
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
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Presence status duration"].Value, columns, rows));
    }

    private ReportDocument BuildBreakAnalysis(
        IReadOnlyList<AgentPresenceInterval> intervals,
        Dictionary<string, AgentProfile> agents)
    {
        var breaks = intervals.Where(interval => interval.Status is AgentPresenceStatus.Break or AgentPresenceStatus.Away);
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
            .Select(entry => entry.Row);

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
            });

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
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(occupancy ? S["Agent occupancy"].Value : S["Agent utilization"].Value, columns, rows));
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
        var rows = intervals
            .Where(interval => !string.IsNullOrWhiteSpace(interval.Reason))
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
            .Select(entry => entry.Row);

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
        bool queueMembership)
    {
        var memberships = intervals
            .Where(interval => interval.Status != AgentPresenceStatus.Offline)
            .SelectMany(interval => (queueMembership ? interval.QueueIds : interval.CampaignIds)
                .Select(id => new { Id = id, interval.DurationSeconds }));
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
                        group.Key,
                        ReportFormat.Duration(duration),
                        ReportFormat.Number(group.LongCount()),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Duration)
            .Select(entry => entry.Row);

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
            });

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
            return agent.DisplayName ?? agent.UserName ?? agent.Name ?? agentId;
        }

        return string.IsNullOrEmpty(agentId) ? "(Unknown)" : agentId;
    }

    internal sealed class AgentPresenceInterval
    {
        public string AgentId { get; set; }

        public AgentPresenceStatus Status { get; set; }

        public string Reason { get; set; }

        public IList<string> QueueIds { get; set; } = [];

        public IList<string> CampaignIds { get; set; } = [];

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public double DurationSeconds => Math.Max(0d, (EndUtc - StartUtc).TotalSeconds);
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

internal sealed record AgentWorkforceReportDefinition(
    string Name,
    Func<LocalizedString> DisplayName,
    Func<LocalizedString> Description,
    AgentWorkforceReportKind Kind,
    string Category);

internal enum AgentWorkforceReportKind
{
    TimeSummary,
    DailyTimecard,
    StatusDuration,
    BreakAnalysis,
    ReadyNotReady,
    Utilization,
    Occupancy,
    ReasonBreakdown,
    PresenceAudit,
    QueueMembershipHours,
    CampaignMembershipHours,
    PayrollTimecard,
}
