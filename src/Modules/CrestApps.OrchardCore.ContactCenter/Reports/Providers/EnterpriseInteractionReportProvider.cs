using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

internal sealed class EnterpriseInteractionReportProvider : IReport, IReportFilterMetadata
{
    private readonly ISession _session;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly EnterpriseInteractionReportDefinition _definition;
    private readonly IStringLocalizer _stringLocalizer;
    private Dictionary<string, string> _agentUserNames = [];

    public EnterpriseInteractionReportProvider(
        ISession session,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        EnterpriseInteractionReportDefinition definition,
        IStringLocalizer stringLocalizer)
    {
        _session = session;
        _queueManager = queueManager;
        _agentManager = agentManager;
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
        var interactions = (await _session.Query<Interaction, InteractionIndex>(
            index => index.CreatedUtc >= context.FromUtc && index.CreatedUtc <= context.ToUtc,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken))
            .ToArray();
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);
        var queues = (await _queueManager.GetAllAsync(cancellationToken))
            .ToDictionary(queue => queue.ItemId, StringComparer.Ordinal);

        ContactCenterReportingService.ApplyCurrentQueueGroupCriteria(criteria, queues.Values.ToArray());
        var filteredInteractions = ContactCenterReportingService.FilterInteractions(interactions, criteria);
        _agentUserNames = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => !string.IsNullOrEmpty(agent.UserName))
            .ToDictionary(agent => agent.ItemId, agent => agent.UserName, StringComparer.Ordinal);

        return _definition.Kind switch
        {
            EnterpriseInteractionReportKind.ExecutiveSummary => BuildExecutiveSummary(filteredInteractions, queues),
            EnterpriseInteractionReportKind.VolumeTrend => BuildIntervalPerformance(filteredInteractions, volumeOnly: true),
            EnterpriseInteractionReportKind.IntervalPerformance => BuildIntervalPerformance(filteredInteractions, volumeOnly: false),
            EnterpriseInteractionReportKind.ChannelPerformance => BuildDimensionPerformance(filteredInteractions, S["Channel"].Value, interaction => interaction.Channel.ToString()),
            EnterpriseInteractionReportKind.DirectionPerformance => BuildDimensionPerformance(filteredInteractions, S["Direction"].Value, interaction => interaction.Direction.ToString()),
            EnterpriseInteractionReportKind.ProviderPerformance => BuildDimensionPerformance(filteredInteractions, S["Provider"].Value, interaction => DisplayOrUnknown(interaction.ProviderName)),
            EnterpriseInteractionReportKind.OutcomePerformance => BuildDimensionPerformance(filteredInteractions, S["Outcome"].Value, interaction => interaction.Status.ToString()),
            EnterpriseInteractionReportKind.InteractionDetail => BuildInteractionDetail(filteredInteractions),
            EnterpriseInteractionReportKind.TransferAnalysis => BuildTransferAnalysis(filteredInteractions),
            EnterpriseInteractionReportKind.RecordingCoverage => BuildRecordingCoverage(filteredInteractions),
            EnterpriseInteractionReportKind.QueueServiceLevel => BuildQueueServiceLevel(filteredInteractions, queues),
            EnterpriseInteractionReportKind.QueueAbandonment => BuildQueueAbandonment(filteredInteractions, queues),
            EnterpriseInteractionReportKind.AgentHandleTime => BuildAgentHandleTime(filteredInteractions),
            EnterpriseInteractionReportKind.WrapUpPerformance => BuildWrapUpPerformance(filteredInteractions),
            EnterpriseInteractionReportKind.HourOfDayPerformance => BuildTimeDimensionPerformance(filteredInteractions, S["Hour (UTC)"].Value, interaction => interaction.CreatedUtc.Hour.ToString("00", CultureInfo.InvariantCulture)),
            EnterpriseInteractionReportKind.DayOfWeekPerformance => BuildTimeDimensionPerformance(filteredInteractions, S["Day of week"].Value, interaction => interaction.CreatedUtc.DayOfWeek.ToString()),
            EnterpriseInteractionReportKind.QueuePerformance => BuildNamedDimensionPerformance(filteredInteractions, queues, queueDimension: true),
            EnterpriseInteractionReportKind.QueueWaitTime => BuildQueueDurationPerformance(filteredInteractions, queues, queueWait: true),
            EnterpriseInteractionReportKind.QueueHandleTime => BuildQueueDurationPerformance(filteredInteractions, queues, queueWait: false),
            EnterpriseInteractionReportKind.QueueTransferPerformance => BuildQueueTransferPerformance(filteredInteractions, queues),
            EnterpriseInteractionReportKind.AgentVolume => BuildAgentPerformance(filteredInteractions, AgentPerformanceMode.Volume),
            EnterpriseInteractionReportKind.AgentOutcome => BuildAgentPerformance(filteredInteractions, AgentPerformanceMode.Outcome),
            EnterpriseInteractionReportKind.AgentInbound => BuildAgentPerformance(filteredInteractions.Where(interaction => interaction.Direction == InteractionDirection.Inbound).ToArray(), AgentPerformanceMode.Volume),
            EnterpriseInteractionReportKind.AgentOutbound => BuildAgentPerformance(filteredInteractions.Where(interaction => interaction.Direction == InteractionDirection.Outbound).ToArray(), AgentPerformanceMode.Volume),
            EnterpriseInteractionReportKind.AgentTransferPerformance => BuildAgentPerformance(filteredInteractions, AgentPerformanceMode.Transfers),
            EnterpriseInteractionReportKind.AgentRecordingCoverage => BuildAgentPerformance(filteredInteractions, AgentPerformanceMode.Recordings),
            EnterpriseInteractionReportKind.QueueUsageBilling => BuildUsageReport(filteredInteractions, S["Queue"].Value, interaction => ResolveQueueName(interaction.QueueId, queues)),
            EnterpriseInteractionReportKind.AgentUsageBilling => BuildUsageReport(filteredInteractions, S["Agent"].Value, interaction => ResolveAgentName(interaction.AgentId)),
            EnterpriseInteractionReportKind.ProviderUsageBilling => BuildUsageReport(filteredInteractions, S["Provider"].Value, interaction => DisplayOrUnknown(interaction.ProviderName)),
            EnterpriseInteractionReportKind.ChannelUsageBilling => BuildUsageReport(filteredInteractions, S["Channel"].Value, interaction => interaction.Channel.ToString()),
            EnterpriseInteractionReportKind.DailyUsageBilling => BuildUsageReport(filteredInteractions, S["Date (UTC)"].Value, interaction => interaction.CreatedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            EnterpriseInteractionReportKind.TranscriptCoverage => BuildReferenceCoverage(filteredInteractions, transcript: true),
            EnterpriseInteractionReportKind.LongInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => GetTalkSeconds(interaction) >= 900d), S["Long interactions (15+ minutes)"].Value),
            EnterpriseInteractionReportKind.FailedInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => interaction.Status == InteractionStatus.Failed), S["Failed interactions"].Value),
            EnterpriseInteractionReportKind.AbandonedInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(IsAbandoned), S["Abandoned interactions"].Value),
            EnterpriseInteractionReportKind.HighWaitDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => GetWaitSeconds(interaction) >= 60d), S["High-wait interactions (60+ seconds)"].Value),
            EnterpriseInteractionReportKind.LifecycleDuration => BuildLifecycleDuration(filteredInteractions),
            EnterpriseInteractionReportKind.CallLegPerformance => BuildCallLegPerformance(filteredInteractions),
            _ => new ReportDocument(),
        };
    }

    private IStringLocalizer S => _stringLocalizer;

    private ReportDocument BuildExecutiveSummary(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var totals = Aggregate(interactions);

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Executive performance"].Value,
            [
                new ReportMetric(S["Interactions"].Value, ReportFormat.Number(totals.Total)),
                new ReportMetric(S["Inbound offered"].Value, ReportFormat.Number(totals.InboundOffered)),
                new ReportMetric(S["Inbound answered"].Value, ReportFormat.Number(totals.InboundAnswered), ReportFormat.Percent(totals.InboundAnswerRate)),
                new ReportMetric(S["Abandoned"].Value, ReportFormat.Number(totals.Abandoned), ReportFormat.Percent(totals.AbandonmentRate)),
                new ReportMetric(S["Failed"].Value, ReportFormat.Number(totals.Failed)),
                new ReportMetric(S["Avg speed of answer"].Value, ReportFormat.Duration(totals.AverageSpeedOfAnswerSeconds)),
                new ReportMetric(S["Avg handle time"].Value, ReportFormat.Duration(totals.AverageHandleTimeSeconds)),
                new ReportMetric(S["Transfer rate"].Value, ReportFormat.Percent(totals.TransferRate)),
                new ReportMetric(S["Recording coverage"].Value, ReportFormat.Percent(totals.RecordingCoverage)),
            ]))
            .Add(BuildDailyTrendChart(interactions))
            .Add(BuildChannelMixChart(interactions))
            .Add(BuildQueueServiceLevelChart(interactions, queues))
            .Add(BuildAgentWorkloadChart(interactions))
            .Add(BuildPerformanceTable(
                S["Channel performance"].Value,
                S["Channel"].Value,
                interactions.GroupBy(interaction => interaction.Channel.ToString())));
    }

    private ReportSection BuildDailyTrendChart(IReadOnlyList<Interaction> interactions)
    {
        var daily = interactions
            .GroupBy(interaction => DateOnly.FromDateTime(interaction.CreatedUtc))
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Metrics = Aggregate(group),
            })
            .ToArray();

        return ReportSection.ForChart(
            S["Daily interaction trend"].Value,
            new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = [.. daily.Select(entry => entry.Date.ToString("MMM d", CultureInfo.InvariantCulture))],
                Datasets =
                [
                    new ReportChartDataset(S["Inbound offered"].Value, daily.Select(entry => (double)entry.Metrics.InboundOffered)),
                    new ReportChartDataset(S["Inbound answered"].Value, daily.Select(entry => (double)entry.Metrics.InboundAnswered)),
                    new ReportChartDataset(S["Abandoned"].Value, daily.Select(entry => (double)entry.Metrics.Abandoned)),
                ],
            },
            width: 8);
    }

    private ReportSection BuildChannelMixChart(IReadOnlyList<Interaction> interactions)
    {
        var channelMix = interactions
            .GroupBy(interaction => interaction.Channel)
            .Select(group => new
            {
                Label = group.Key.ToString(),
                Count = group.LongCount(),
            })
            .OrderByDescending(entry => entry.Count)
            .ToArray();

        return ReportSection.ForChart(
            S["Channel mix"].Value,
            new ReportChart
            {
                Type = ReportChartType.Doughnut,
                Labels = [.. channelMix.Select(entry => entry.Label)],
                Datasets =
                [
                    new ReportChartDataset(S["Interactions"].Value, channelMix.Select(entry => (double)entry.Count)),
                ],
            },
            width: 4);
    }

    private ReportSection BuildQueueServiceLevelChart(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var queuePerformance = interactions
            .Where(IsInboundOffered)
            .GroupBy(interaction => interaction.QueueId ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                queues.TryGetValue(group.Key, out var queue);
                var metrics = CalculateQueueServiceLevel(group, queue?.SlaThresholdSeconds ?? 0);

                return new
                {
                    Label = queue?.Name ?? DisplayOrUnknown(group.Key),
                    Offered = metrics.EligibleOffered,
                    ServiceLevel = metrics.ServiceLevel * 100d,
                    metrics.HasServiceLevel,
                };
            })
            .Where(entry => entry.HasServiceLevel)
            .OrderByDescending(entry => entry.Offered)
            .Take(10)
            .ToArray();

        return ReportSection.ForChart(
            S["Queue service level"].Value,
            new ReportChart
            {
                Type = ReportChartType.Bar,
                Labels = [.. queuePerformance.Select(entry => entry.Label)],
                Datasets =
                [
                    new ReportChartDataset(S["Service level"].Value, queuePerformance.Select(entry => entry.ServiceLevel)),
                ],
                PercentageScale = true,
                ShowLegend = false,
            });
    }

    private ReportSection BuildAgentWorkloadChart(IReadOnlyList<Interaction> interactions)
    {
        var agentWorkload = interactions
            .Where(interaction => interaction.AnsweredUtc.HasValue && !string.IsNullOrEmpty(interaction.AgentId))
            .GroupBy(interaction => interaction.AgentId, StringComparer.Ordinal)
            .Select(group => new
            {
                Agent = group.Key,
                Handled = group.LongCount(),
            })
            .OrderByDescending(entry => entry.Handled)
            .Take(10)
            .ToArray();

        return ReportSection.ForChart(
            S["Top agent workload"].Value,
            new ReportChart
            {
                Type = ReportChartType.Bar,
                Labels = [.. agentWorkload.Select(entry => ResolveAgentName(entry.Agent))],
                Datasets =
                [
                    new ReportChartDataset(S["Handled"].Value, agentWorkload.Select(entry => (double)entry.Handled)),
                ],
                ShowLegend = false,
            });
    }

    private ReportDocument BuildIntervalPerformance(IReadOnlyList<Interaction> interactions, bool volumeOnly)
    {
        ReportColumn[] columns = volumeOnly
            ?
            [
                new ReportColumn(S["Date (UTC)"].Value),
                new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            ]
            :
            [
                new ReportColumn(S["Date (UTC)"].Value),
                new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Answer rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Abandonment rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg speed of answer"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
            ];

        var rows = new List<ReportRow>();

        foreach (var group in interactions
            .GroupBy(interaction => DateOnly.FromDateTime(interaction.CreatedUtc))
            .OrderBy(group => group.Key))
        {
            var metrics = Aggregate(group);
            var cells = new List<string>
            {
                group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ReportFormat.Number(metrics.Total),
                ReportFormat.Number(metrics.Answered),
                ReportFormat.Number(metrics.Abandoned),
            };

            if (volumeOnly)
            {
                cells.Add(ReportFormat.Number(metrics.Failed));
            }
            else
            {
                cells.Add(ReportFormat.Percent(metrics.AnswerRate));
                cells.Add(ReportFormat.Percent(metrics.AbandonmentRate));
                cells.Add(ReportFormat.Duration(metrics.AverageSpeedOfAnswerSeconds));
                cells.Add(ReportFormat.Duration(metrics.AverageHandleTimeSeconds));
            }

            rows.Add(new ReportRow(cells));
        }

        var totals = Aggregate(interactions);
        var totalCells = new List<string>
        {
            S["All dates"].Value,
            ReportFormat.Number(totals.Total),
            ReportFormat.Number(totals.Answered),
            ReportFormat.Number(totals.Abandoned),
        };

        if (volumeOnly)
        {
            totalCells.Add(ReportFormat.Number(totals.Failed));
        }
        else
        {
            totalCells.Add(ReportFormat.Percent(totals.AnswerRate));
            totalCells.Add(ReportFormat.Percent(totals.AbandonmentRate));
            totalCells.Add(ReportFormat.Duration(totals.AverageSpeedOfAnswerSeconds));
            totalCells.Add(ReportFormat.Duration(totals.AverageHandleTimeSeconds));
        }

        rows.Add(new ReportRow(totalCells, ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Daily performance"].Value, columns, rows));
    }

    private ReportDocument BuildDimensionPerformance(
        IReadOnlyList<Interaction> interactions,
        string dimensionName,
        Func<Interaction, string> selector)
    {
        return new ReportDocument()
            .Add(BuildPerformanceTable(
                S["Performance"].Value,
                dimensionName,
                interactions.GroupBy(selector, StringComparer.OrdinalIgnoreCase)));
    }

    private ReportSection BuildPerformanceTable(
        string title,
        string dimensionName,
        IEnumerable<IGrouping<string, Interaction>> groups)
    {
        var columns = new[]
        {
            new ReportColumn(dimensionName),
            new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answer rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandonment rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg speed of answer"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
        };

        var grouped = groups.ToArray();
        var rows = grouped
            .Select(group => new { Label = group.Key, Metrics = Aggregate(group) })
            .OrderByDescending(entry => entry.Metrics.Total)
            .Select(entry => CreatePerformanceRow(entry.Label, entry.Metrics, ReportRowKind.Detail))
            .ToList();

        rows.Add(CreatePerformanceRow(
            S["Grand total"].Value,
            Aggregate(grouped.SelectMany(group => group)),
            ReportRowKind.GrandTotal));

        return ReportSection.ForTable(title, columns, rows);
    }

    internal static ReportRow CreatePerformanceRow(
        string label,
        InteractionMetrics metrics,
        ReportRowKind kind)
    {
        return new ReportRow(
        [
            label,
            ReportFormat.Number(metrics.Total),
            ReportFormat.Number(metrics.Answered),
            ReportFormat.Number(metrics.Abandoned),
            ReportFormat.Number(metrics.Failed),
            ReportFormat.Percent(metrics.AnswerRate),
            ReportFormat.Percent(metrics.AbandonmentRate),
            ReportFormat.Duration(metrics.AverageSpeedOfAnswerSeconds),
            ReportFormat.Duration(metrics.AverageHandleTimeSeconds),
        ], kind);
    }

    private ReportDocument BuildInteractionDetail(IReadOnlyList<Interaction> interactions, string title = null)
    {
        var columns = new[]
        {
            new ReportColumn(S["Started (UTC)"].Value),
            new ReportColumn(S["Interaction"].Value),
            new ReportColumn(S["Channel"].Value),
            new ReportColumn(S["Direction"].Value),
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Provider"].Value),
            new ReportColumn(S["Wait"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Connected"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Wrap-up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transfers"].Value, ReportColumnAlign.End),
        };

        var rows = interactions
            .OrderByDescending(interaction => interaction.CreatedUtc)
            .Select(interaction => new ReportRow(
            [
                interaction.CreatedUtc.ToString("u", CultureInfo.InvariantCulture),
                interaction.ItemId,
                interaction.Channel.ToString(),
                interaction.Direction.ToString(),
                interaction.Status.ToString(),
                DisplayOrUnknown(interaction.QueueId),
                ResolveAgentName(interaction.AgentId),
                DisplayOrUnknown(interaction.ProviderName),
                ReportFormat.Duration(GetWaitSeconds(interaction)),
                ReportFormat.Duration(GetTalkSeconds(interaction)),
                ReportFormat.Duration(GetWrapUpSeconds(interaction)),
                ReportFormat.Number(interaction.TransferHistory.Count),
            ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(title ?? S["Interactions"].Value, columns, rows));
    }

    private ReportDocument BuildTransferAnalysis(IReadOnlyList<Interaction> interactions)
    {
        var transfers = interactions.SelectMany(interaction => interaction.TransferHistory).ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Target type"].Value),
            new ReportColumn(S["Result"].Value),
            new ReportColumn(S["Transfers"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg completion time"].Value, ReportColumnAlign.End),
        };

        var rows = transfers
            .GroupBy(transfer => new
            {
                TargetType = DisplayOrUnknown(transfer.TargetType),
                Result = DisplayOrUnknown(transfer.Result),
            })
            .Select(group =>
            {
                var completed = group.Where(transfer => transfer.CompletedUtc.HasValue).ToArray();
                var averageSeconds = completed.Length == 0
                    ? 0d
                    : completed.Average(transfer => Math.Max(0d, (transfer.CompletedUtc.Value - transfer.RequestedUtc).TotalSeconds));

                return new
                {
                    Count = group.LongCount(),
                    Row = new ReportRow(
                    [
                        group.Key.TargetType,
                        group.Key.Result,
                        ReportFormat.Number(group.LongCount()),
                        ReportFormat.Number(completed.LongLength),
                        ReportFormat.Percent(group.Any() ? (double)completed.LongLength / group.LongCount() : 0d),
                        ReportFormat.Duration(averageSeconds),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Count)
            .Select(entry => entry.Row)
            .ToList();

        var completedTransfers = transfers.Where(transfer => transfer.CompletedUtc.HasValue).ToArray();
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            "—",
            ReportFormat.Number(transfers.LongLength),
            ReportFormat.Number(completedTransfers.LongLength),
            ReportFormat.Percent(transfers.Length > 0 ? (double)completedTransfers.LongLength / transfers.LongLength : 0d),
            ReportFormat.Duration(completedTransfers.Length > 0
                ? completedTransfers.Average(transfer => Math.Max(0d, (transfer.CompletedUtc.Value - transfer.RequestedUtc).TotalSeconds))
                : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Transfer outcomes"].Value, columns, rows));
    }

    private ReportDocument BuildRecordingCoverage(IReadOnlyList<Interaction> interactions)
    {
        var voice = interactions
            .Where(interaction => interaction.Channel == InteractionChannel.Voice && interaction.AnsweredUtc.HasValue)
            .ToArray();

        var columns = new[]
        {
            new ReportColumn(S["Provider"].Value),
            new ReportColumn(S["Answered voice interactions"].Value, ReportColumnAlign.End),
            new ReportColumn(S["With recording"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Without recording"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Coverage"].Value, ReportColumnAlign.End),
        };

        var rows = voice
            .GroupBy(interaction => DisplayOrUnknown(interaction.ProviderName), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var answered = group.LongCount();
                var recorded = group.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference));

                return new
                {
                    Coverage = answered > 0 ? (double)recorded / answered : 0d,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(answered),
                        ReportFormat.Number(recorded),
                        ReportFormat.Number(answered - recorded),
                        ReportFormat.Percent(answered > 0 ? (double)recorded / answered : 0d),
                    ]),
                };
            })
            .OrderBy(entry => entry.Coverage)
            .Select(entry => entry.Row)
            .ToList();

        var recorded = voice.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference));
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(voice.LongLength),
            ReportFormat.Number(recorded),
            ReportFormat.Number(voice.LongLength - recorded),
            ReportFormat.Percent(voice.Length > 0 ? (double)recorded / voice.LongLength : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Recording coverage"].Value, columns, rows));
    }

    private ReportDocument BuildQueueServiceLevel(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["SLA threshold"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Eligible offered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered within SLA"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Service level"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg speed of answer"].Value, ReportColumnAlign.End),
        };

        var rows = interactions
            .Where(IsInboundOffered)
            .GroupBy(interaction => interaction.QueueId ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                queues.TryGetValue(group.Key, out var queue);
                var threshold = queue?.SlaThresholdSeconds ?? 0;
                var metrics = CalculateQueueServiceLevel(group, threshold);

                return new
                {
                    metrics.HasServiceLevel,
                    metrics.ServiceLevel,
                    Row = new ReportRow(
                    [
                        queue?.Name ?? DisplayOrUnknown(group.Key),
                        threshold > 0 ? ReportFormat.Duration(threshold) : "—",
                        ReportFormat.Number(metrics.EligibleOffered),
                        ReportFormat.Number(metrics.AnsweredWithinThreshold),
                        metrics.HasServiceLevel ? ReportFormat.Percent(metrics.ServiceLevel) : "—",
                        ReportFormat.Duration(metrics.AverageSpeedOfAnswerSeconds),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.HasServiceLevel)
            .ThenBy(entry => entry.ServiceLevel)
            .Select(entry => entry.Row)
            .ToList();

        var totals = CalculateCombinedQueueServiceLevel(interactions, queues);
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            "—",
            ReportFormat.Number(totals.EligibleOffered),
            ReportFormat.Number(totals.AnsweredWithinThreshold),
            totals.HasServiceLevel ? ReportFormat.Percent(totals.ServiceLevel) : "—",
            ReportFormat.Duration(totals.AverageSpeedOfAnswerSeconds),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queue service level"].Value, columns, rows));
    }

    private ReportDocument BuildQueueAbandonment(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Offered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandonment rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg wait before abandon"].Value, ReportColumnAlign.End),
        };

        var offeredInteractions = interactions.Where(IsInboundOffered).ToArray();
        var rows = offeredInteractions
            .GroupBy(interaction => interaction.QueueId ?? string.Empty, StringComparer.Ordinal)
            .Select(group =>
            {
                queues.TryGetValue(group.Key, out var queue);
                var offered = group.LongCount();
                var answered = group.LongCount(interaction => interaction.AnsweredUtc.HasValue);
                var abandoned = group.Where(IsAbandoned).ToArray();

                var abandonmentRate = offered > 0 ? (double)abandoned.LongLength / offered : 0d;

                return new
                {
                    AbandonmentRate = abandonmentRate,
                    Row = new ReportRow(
                    [
                        queue?.Name ?? DisplayOrUnknown(group.Key),
                        ReportFormat.Number(offered),
                        ReportFormat.Number(answered),
                        ReportFormat.Number(abandoned.LongLength),
                        ReportFormat.Percent(abandonmentRate),
                        ReportFormat.Duration(abandoned.Length > 0 ? abandoned.Average(GetWaitUntilEndSeconds) : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.AbandonmentRate)
            .Select(entry => entry.Row)
            .ToList();

        var answeredTotal = offeredInteractions.LongCount(interaction => interaction.AnsweredUtc.HasValue);
        var abandonedTotal = offeredInteractions.Where(IsAbandoned).ToArray();
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(offeredInteractions.LongLength),
            ReportFormat.Number(answeredTotal),
            ReportFormat.Number(abandonedTotal.LongLength),
            ReportFormat.Percent(offeredInteractions.Length > 0
                ? (double)abandonedTotal.LongLength / offeredInteractions.LongLength
                : 0d),
            ReportFormat.Duration(abandonedTotal.Length > 0 ? abandonedTotal.Average(GetWaitUntilEndSeconds) : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queue abandonment"].Value, columns, rows));
    }

    private ReportDocument BuildAgentHandleTime(IReadOnlyList<Interaction> interactions)
    {
        var answered = interactions.Where(interaction =>
            interaction.AnsweredUtc.HasValue &&
            interaction.EndedUtc.HasValue &&
            interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value &&
            !string.IsNullOrEmpty(interaction.AgentId));

        return new ReportDocument()
            .Add(BuildAgentTimeTable(S["Agent handle time"].Value, answered, includeWrapUpOnly: false));
    }

    private ReportDocument BuildWrapUpPerformance(IReadOnlyList<Interaction> interactions)
    {
        var wrapped = interactions.Where(interaction => !string.IsNullOrEmpty(interaction.AgentId) && interaction.WrapUpStartedUtc.HasValue);

        return new ReportDocument()
            .Add(BuildAgentTimeTable(S["Agent wrap-up performance"].Value, wrapped, includeWrapUpOnly: true));
    }

    private ReportSection BuildAgentTimeTable(string title, IEnumerable<Interaction> interactions, bool includeWrapUpOnly)
    {
        ReportColumn[] columns = includeWrapUpOnly
            ?
            [
                new ReportColumn(S["Agent"].Value),
                new ReportColumn(S["Wrap-up started"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Wrap-up completed"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Completion rate"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg wrap-up"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Total wrap-up"].Value, ReportColumnAlign.End),
            ]
            :
            [
                new ReportColumn(S["Agent"].Value),
                new ReportColumn(S["Handled"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg connected"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg wrap-up"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Total handle time"].Value, ReportColumnAlign.End),
            ];

        var population = interactions.ToArray();
        var rows = population
            .GroupBy(interaction => interaction.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var count = group.LongCount();
                var completed = group.LongCount(interaction => interaction.WrapUpCompletedUtc.HasValue);
                var talk = group.Sum(GetTalkSeconds);
                var wrapUp = group.Sum(GetWrapUpSeconds);

                var average = includeWrapUpOnly
                    ? completed > 0 ? wrapUp / completed : 0d
                    : count > 0 ? (talk + wrapUp) / count : 0d;

                return new
                {
                    Average = average,
                    Row = includeWrapUpOnly
                        ? new ReportRow(
                        [
                            ResolveAgentName(group.Key),
                            ReportFormat.Number(count),
                            ReportFormat.Number(completed),
                            ReportFormat.Percent(count > 0 ? (double)completed / count : 0d),
                            ReportFormat.Duration(average),
                            ReportFormat.Duration(wrapUp),
                        ])
                        : new ReportRow(
                        [
                            ResolveAgentName(group.Key),
                            ReportFormat.Number(count),
                            ReportFormat.Duration(count > 0 ? talk / count : 0d),
                            ReportFormat.Duration(count > 0 ? wrapUp / count : 0d),
                            ReportFormat.Duration(average),
                            ReportFormat.Duration(talk + wrapUp),
                        ]),
                };
            })
            .OrderByDescending(entry => entry.Average)
            .Select(entry => entry.Row)
            .ToList();

        var count = population.LongLength;
        var completed = population.LongCount(interaction => interaction.WrapUpCompletedUtc.HasValue);
        var talk = population.Sum(GetTalkSeconds);
        var wrapUp = population.Sum(GetWrapUpSeconds);
        var average = includeWrapUpOnly
            ? completed > 0 ? wrapUp / completed : 0d
            : count > 0 ? (talk + wrapUp) / count : 0d;

        rows.Add(includeWrapUpOnly
            ? new ReportRow(
            [
                S["Grand total"].Value,
                ReportFormat.Number(count),
                ReportFormat.Number(completed),
                ReportFormat.Percent(count > 0 ? (double)completed / count : 0d),
                ReportFormat.Duration(average),
                ReportFormat.Duration(wrapUp),
            ], ReportRowKind.GrandTotal)
            : new ReportRow(
            [
                S["Grand total"].Value,
                ReportFormat.Number(count),
                ReportFormat.Duration(count > 0 ? talk / count : 0d),
                ReportFormat.Duration(count > 0 ? wrapUp / count : 0d),
                ReportFormat.Duration(average),
                ReportFormat.Duration(talk + wrapUp),
            ], ReportRowKind.GrandTotal));

        return ReportSection.ForTable(title, columns, rows);
    }

    private ReportDocument BuildTimeDimensionPerformance(
        IReadOnlyList<Interaction> interactions,
        string dimensionName,
        Func<Interaction, string> selector)
    {
        return new ReportDocument()
            .Add(BuildPerformanceTable(
                S["Interaction performance"].Value,
                dimensionName,
                interactions.GroupBy(selector, StringComparer.OrdinalIgnoreCase)));
    }

    private ReportDocument BuildNamedDimensionPerformance(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues,
        bool queueDimension)
    {
        return new ReportDocument()
            .Add(BuildPerformanceTable(
                S["Queue performance"].Value,
                queueDimension ? S["Queue"].Value : S["Dimension"].Value,
                interactions.GroupBy(interaction => ResolveQueueName(interaction.QueueId, queues), StringComparer.OrdinalIgnoreCase)));
    }

    private ReportDocument BuildQueueDurationPerformance(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues,
        bool queueWait)
    {
        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
            new ReportColumn(queueWait ? S["Total wait"].Value : S["Total handle time"].Value, ReportColumnAlign.End),
            new ReportColumn(queueWait ? S["Average wait"].Value : S["Average handle time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Maximum"].Value, ReportColumnAlign.End),
        };
        var rows = interactions
            .GroupBy(interaction => ResolveQueueName(interaction.QueueId, queues), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var durations = group
                    .Select(interaction => queueWait ? GetWaitSeconds(interaction) : GetTalkSeconds(interaction) + GetWrapUpSeconds(interaction))
                    .ToArray();
                var total = durations.Sum();

                return new
                {
                    Total = total,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(durations.LongLength),
                        ReportFormat.Duration(total),
                        ReportFormat.Duration(durations.Length > 0 ? total / durations.Length : 0d),
                        ReportFormat.Duration(durations.Length > 0 ? durations.Max() : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Total)
            .Select(entry => entry.Row)
            .ToList();

        var durations = interactions
            .Select(interaction => queueWait ? GetWaitSeconds(interaction) : GetTalkSeconds(interaction) + GetWrapUpSeconds(interaction))
            .ToArray();
        var total = durations.Sum();

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(durations.LongLength),
            ReportFormat.Duration(total),
            ReportFormat.Duration(durations.Length > 0 ? total / durations.Length : 0d),
            ReportFormat.Duration(durations.Length > 0 ? durations.Max() : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(queueWait ? S["Queue wait time"].Value : S["Queue handle time"].Value, columns, rows));
    }

    private ReportDocument BuildQueueTransferPerformance(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Handled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transferred"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transfers"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transfer rate"].Value, ReportColumnAlign.End),
        };
        var handledInteractions = interactions.Where(interaction => interaction.AnsweredUtc.HasValue).ToArray();
        var rows = handledInteractions
            .GroupBy(interaction => ResolveQueueName(interaction.QueueId, queues), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var handled = group.LongCount();
                var transferred = group.LongCount(interaction => interaction.TransferHistory.Count > 0);
                var transfers = group.Sum(interaction => interaction.TransferHistory.Count);

                return new
                {
                    Transfers = transfers,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(handled),
                        ReportFormat.Number(transferred),
                        ReportFormat.Number(transfers),
                        ReportFormat.Percent(handled > 0 ? (double)transferred / handled : 0d),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Transfers)
            .Select(entry => entry.Row)
            .ToList();

        var transferred = handledInteractions.LongCount(interaction => interaction.TransferHistory.Count > 0);
        var transfers = handledInteractions.Sum(interaction => interaction.TransferHistory.Count);
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(handledInteractions.LongLength),
            ReportFormat.Number(transferred),
            ReportFormat.Number(transfers),
            ReportFormat.Percent(handledInteractions.Length > 0
                ? (double)transferred / handledInteractions.LongLength
                : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queue transfer performance"].Value, columns, rows));
    }

    private ReportDocument BuildAgentPerformance(
        IReadOnlyList<Interaction> interactions,
        AgentPerformanceMode mode)
    {
        var columns = new[]
        {
            new ReportColumn(S["Agent"].Value),
            new ReportColumn(S["Handled"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Failed"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transfers"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Recorded"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Recording coverage"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg handle time"].Value, ReportColumnAlign.End),
        };
        var population = interactions.Where(interaction => !string.IsNullOrEmpty(interaction.AgentId)).ToArray();
        var rows = population
            .GroupBy(interaction => interaction.AgentId, StringComparer.Ordinal)
            .Select(group =>
            {
                var metrics = Aggregate(group);
                var transfers = group.Sum(interaction => interaction.TransferHistory.Count);
                var answeredVoice = group.LongCount(interaction => interaction.Channel == InteractionChannel.Voice && interaction.AnsweredUtc.HasValue);
                var recorded = group.LongCount(interaction =>
                    interaction.Channel == InteractionChannel.Voice &&
                    interaction.AnsweredUtc.HasValue &&
                    !string.IsNullOrEmpty(interaction.RecordingReference));
                var order = mode switch
                {
                    AgentPerformanceMode.Transfers => transfers,
                    AgentPerformanceMode.Recordings => recorded,
                    AgentPerformanceMode.Outcome => metrics.Failed,
                    _ => metrics.Total,
                };

                return new
                {
                    Order = (double)order,
                    Row = new ReportRow(
                    [
                        ResolveAgentName(group.Key),
                        ReportFormat.Number(metrics.Total),
                        ReportFormat.Number(metrics.Answered),
                        ReportFormat.Number(metrics.Failed),
                        ReportFormat.Number(transfers),
                        ReportFormat.Number(recorded),
                        ReportFormat.Percent(answeredVoice > 0 ? (double)recorded / answeredVoice : 0d),
                        ReportFormat.Duration(metrics.AverageHandleTimeSeconds),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.Order)
            .Select(entry => entry.Row)
            .ToList();

        var totals = Aggregate(population);
        var totalTransfers = population.Sum(interaction => interaction.TransferHistory.Count);
        var totalAnsweredVoice = population.LongCount(interaction =>
            interaction.Channel == InteractionChannel.Voice &&
            interaction.AnsweredUtc.HasValue);
        var totalRecorded = population.LongCount(interaction =>
            interaction.Channel == InteractionChannel.Voice &&
            interaction.AnsweredUtc.HasValue &&
            !string.IsNullOrEmpty(interaction.RecordingReference));

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(totals.Total),
            ReportFormat.Number(totals.Answered),
            ReportFormat.Number(totals.Failed),
            ReportFormat.Number(totalTransfers),
            ReportFormat.Number(totalRecorded),
            ReportFormat.Percent(totalAnsweredVoice > 0 ? (double)totalRecorded / totalAnsweredVoice : 0d),
            ReportFormat.Duration(totals.AverageHandleTimeSeconds),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agent performance"].Value, columns, rows));
    }

    private ReportDocument BuildUsageReport(
        IReadOnlyList<Interaction> interactions,
        string dimensionName,
        Func<Interaction, string> selector)
    {
        var columns = new[]
        {
            new ReportColumn(dimensionName),
            new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Connected time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Wrap-up time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Queue wait time"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Transfers"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Recordings"].Value, ReportColumnAlign.End),
        };
        var rows = interactions
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var connectedSeconds = group.Sum(GetTalkSeconds);

                return new
                {
                    ConnectedSeconds = connectedSeconds,
                    Row = new ReportRow(
                    [
                        group.Key,
                        ReportFormat.Number(group.LongCount()),
                        ReportFormat.Number(group.LongCount(interaction => interaction.AnsweredUtc.HasValue)),
                        ReportFormat.Duration(connectedSeconds),
                        ReportFormat.Duration(group.Sum(GetWrapUpSeconds)),
                        ReportFormat.Duration(group.Sum(GetWaitSeconds)),
                        ReportFormat.Number(group.Sum(interaction => interaction.TransferHistory.Count)),
                        ReportFormat.Number(group.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference))),
                    ]),
                };
            })
            .OrderByDescending(entry => entry.ConnectedSeconds)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(interactions.Count),
            ReportFormat.Number(interactions.LongCount(interaction => interaction.AnsweredUtc.HasValue)),
            ReportFormat.Duration(interactions.Sum(GetTalkSeconds)),
            ReportFormat.Duration(interactions.Sum(GetWrapUpSeconds)),
            ReportFormat.Duration(interactions.Sum(GetWaitSeconds)),
            ReportFormat.Number(interactions.Sum(interaction => interaction.TransferHistory.Count)),
            ReportFormat.Number(interactions.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference))),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Usage summary"].Value, columns, rows));
    }

    private ReportDocument BuildReferenceCoverage(IReadOnlyList<Interaction> interactions, bool transcript)
    {
        var eligible = interactions.Where(interaction => interaction.AnsweredUtc.HasValue).ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Channel"].Value),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(transcript ? S["With transcript"].Value : S["With recording"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Without reference"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Coverage"].Value, ReportColumnAlign.End),
        };
        var rows = eligible
            .GroupBy(interaction => interaction.Channel)
            .Select(group =>
            {
                var answered = group.LongCount();
                var covered = group.LongCount(interaction => transcript
                    ? !string.IsNullOrEmpty(interaction.TranscriptReference)
                    : !string.IsNullOrEmpty(interaction.RecordingReference));

                return new ReportRow(
                [
                    group.Key.ToString(),
                    ReportFormat.Number(answered),
                    ReportFormat.Number(covered),
                    ReportFormat.Number(answered - covered),
                    ReportFormat.Percent(answered > 0 ? (double)covered / answered : 0d),
                ]);
            })
            .ToList();

        var covered = eligible.LongCount(interaction => transcript
            ? !string.IsNullOrEmpty(interaction.TranscriptReference)
            : !string.IsNullOrEmpty(interaction.RecordingReference));
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(eligible.LongLength),
            ReportFormat.Number(covered),
            ReportFormat.Number(eligible.LongLength - covered),
            ReportFormat.Percent(eligible.Length > 0 ? (double)covered / eligible.LongLength : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(transcript ? S["Transcript coverage"].Value : S["Recording coverage"].Value, columns, rows));
    }

    private ReportDocument BuildExceptionDetail(IEnumerable<Interaction> interactions, string title)
    {
        return BuildInteractionDetail(interactions.ToArray(), title);
    }

    private ReportDocument BuildLifecycleDuration(IReadOnlyList<Interaction> interactions)
    {
        var columns = new[]
        {
            new ReportColumn(S["Status"].Value),
            new ReportColumn(S["Interactions"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average wait"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average connected"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average wrap-up"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average end-to-end"].Value, ReportColumnAlign.End),
        };
        var rows = interactions
            .GroupBy(interaction => interaction.Status)
            .Select(group =>
            {
                var count = group.LongCount();
                var ended = group.Where(interaction => interaction.EndedUtc.HasValue).ToArray();

                return new ReportRow(
                [
                    group.Key.ToString(),
                    ReportFormat.Number(count),
                    ReportFormat.Duration(count > 0 ? group.Sum(GetWaitSeconds) / count : 0d),
                    ReportFormat.Duration(count > 0 ? group.Sum(GetTalkSeconds) / count : 0d),
                    ReportFormat.Duration(count > 0 ? group.Sum(GetWrapUpSeconds) / count : 0d),
                    ReportFormat.Duration(ended.Length > 0 ? ended.Average(interaction => Math.Max(0d, (interaction.EndedUtc.Value - interaction.CreatedUtc).TotalSeconds)) : 0d),
                ]);
            })
            .ToList();

        var ended = interactions.Where(interaction => interaction.EndedUtc.HasValue).ToArray();
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(interactions.Count),
            ReportFormat.Duration(interactions.Count > 0 ? interactions.Sum(GetWaitSeconds) / interactions.Count : 0d),
            ReportFormat.Duration(interactions.Count > 0 ? interactions.Sum(GetTalkSeconds) / interactions.Count : 0d),
            ReportFormat.Duration(interactions.Count > 0 ? interactions.Sum(GetWrapUpSeconds) / interactions.Count : 0d),
            ReportFormat.Duration(ended.Length > 0
                ? ended.Average(interaction => Math.Max(0d, (interaction.EndedUtc.Value - interaction.CreatedUtc).TotalSeconds))
                : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Interaction lifecycle duration"].Value, columns, rows));
    }

    private ReportDocument BuildCallLegPerformance(IReadOnlyList<Interaction> interactions)
    {
        var legs = interactions.SelectMany(interaction => interaction.CallLegs).ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Leg status"].Value),
            new ReportColumn(S["Legs"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average duration"].Value, ReportColumnAlign.End),
        };
        var rows = legs
            .GroupBy(leg => DisplayOrUnknown(leg.Status), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ended = group.Where(leg => leg.EndedUtc.HasValue).ToArray();

                return new ReportRow(
                [
                    group.Key,
                    ReportFormat.Number(group.LongCount()),
                    ReportFormat.Number(group.LongCount(leg => leg.AnsweredUtc.HasValue)),
                    ReportFormat.Duration(ended.Length > 0 ? ended.Average(leg => Math.Max(0d, (leg.EndedUtc.Value - leg.StartedUtc).TotalSeconds)) : 0d),
                ]);
            })
            .ToList();

        var endedLegs = legs.Where(leg => leg.EndedUtc.HasValue).ToArray();
        rows.Add(new ReportRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(legs.LongLength),
            ReportFormat.Number(legs.LongCount(leg => leg.AnsweredUtc.HasValue)),
            ReportFormat.Duration(endedLegs.Length > 0
                ? endedLegs.Average(leg => Math.Max(0d, (leg.EndedUtc.Value - leg.StartedUtc).TotalSeconds))
                : 0d),
        ], ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Call leg performance"].Value, columns, rows));
    }

    private string ResolveQueueName(string queueId, Dictionary<string, ActivityQueue> queues)
    {
        return !string.IsNullOrEmpty(queueId) && queues.TryGetValue(queueId, out var queue)
            ? queue.Name
            : DisplayOrUnknown(queueId);
    }

    private enum AgentPerformanceMode
    {
        Volume,
        Outcome,
        Transfers,
        Recordings,
    }

    internal static InteractionMetrics Aggregate(IEnumerable<Interaction> interactions)
    {
        var metrics = new InteractionMetrics();

        foreach (var interaction in interactions)
        {
            metrics.Total++;

            if (IsInboundOffered(interaction))
            {
                metrics.InboundOffered++;
            }

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.Answered++;

                if (interaction.Direction == InteractionDirection.Inbound)
                {
                    metrics.InboundAnswered++;
                    metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);
                }

                if (interaction.EndedUtc.HasValue && interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value)
                {
                    metrics.Handled++;
                    metrics.TalkSeconds += GetTalkSeconds(interaction);
                    metrics.WrapUpSeconds += GetWrapUpSeconds(interaction);
                }

                if (interaction.TransferHistory.Count > 0)
                {
                    metrics.Transferred++;
                }

                if (interaction.Channel == InteractionChannel.Voice)
                {
                    metrics.AnsweredVoice++;

                    if (!string.IsNullOrEmpty(interaction.RecordingReference))
                    {
                        metrics.RecordedVoice++;
                    }
                }
            }

            if (IsAbandoned(interaction))
            {
                metrics.Abandoned++;
            }

            if (interaction.Status == InteractionStatus.Failed)
            {
                metrics.Failed++;
            }
        }

        return metrics;
    }

    internal static QueueServiceLevelMetrics CalculateQueueServiceLevel(IEnumerable<Interaction> interactions, int thresholdSeconds)
    {
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.EligibleOffered++;
                metrics.Answered++;
                metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;

                    if (GetWaitSeconds(interaction) <= thresholdSeconds)
                    {
                        metrics.AnsweredWithinThreshold++;
                    }
                }
            }
            else if (IsAbandoned(interaction))
            {
                metrics.EligibleOffered++;

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;
                }
            }
        }

        metrics.HasServiceLevel = metrics.ServiceLevelEligibleOffered > 0;

        return metrics;
    }

    internal static QueueServiceLevelMetrics CalculateCombinedQueueServiceLevel(
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, ActivityQueue> queues)
    {
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            queues.TryGetValue(interaction.QueueId ?? string.Empty, out var queue);
            var thresholdSeconds = queue?.SlaThresholdSeconds ?? 0;

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.EligibleOffered++;
                metrics.Answered++;
                metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;

                    if (GetWaitSeconds(interaction) <= thresholdSeconds)
                    {
                        metrics.AnsweredWithinThreshold++;
                    }
                }
            }
            else if (IsAbandoned(interaction))
            {
                metrics.EligibleOffered++;

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;
                }
            }
        }

        metrics.HasServiceLevel = metrics.ServiceLevelEligibleOffered > 0;

        return metrics;
    }

    private static bool IsInboundOffered(Interaction interaction)
    {
        return interaction.Direction == InteractionDirection.Inbound;
    }

    private static bool IsAbandoned(Interaction interaction)
    {
        return interaction.Direction == InteractionDirection.Inbound &&
            !interaction.AnsweredUtc.HasValue &&
            interaction.Status == InteractionStatus.Ended;
    }

    private static double GetWaitSeconds(Interaction interaction)
    {
        return interaction.AnsweredUtc.HasValue
            ? Math.Max(0d, (interaction.AnsweredUtc.Value - interaction.CreatedUtc).TotalSeconds)
            : 0d;
    }

    private static double GetWaitUntilEndSeconds(Interaction interaction)
    {
        return interaction.EndedUtc.HasValue
            ? Math.Max(0d, (interaction.EndedUtc.Value - interaction.CreatedUtc).TotalSeconds)
            : 0d;
    }

    private static double GetTalkSeconds(Interaction interaction)
    {
        return interaction.AnsweredUtc.HasValue &&
            interaction.EndedUtc.HasValue &&
            interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value
            ? (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds
            : 0d;
    }

    private static double GetWrapUpSeconds(Interaction interaction)
    {
        return interaction.WrapUpStartedUtc.HasValue &&
            interaction.WrapUpCompletedUtc.HasValue &&
            interaction.WrapUpCompletedUtc.Value >= interaction.WrapUpStartedUtc.Value
            ? (interaction.WrapUpCompletedUtc.Value - interaction.WrapUpStartedUtc.Value).TotalSeconds
            : 0d;
    }

    private string DisplayOrUnknown(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? S["(Not set)"].Value : value;
    }

    private string ResolveAgentName(string agentId)
    {
        if (string.IsNullOrEmpty(agentId) ||
            !_agentUserNames.TryGetValue(agentId, out var userName))
        {
            return S["(Unknown agent)"].Value;
        }

        return ReportValue.UserDisplayName(userName, S["(Unknown agent)"].Value);
    }

    internal sealed class InteractionMetrics
    {
        public long Total { get; set; }

        public long InboundOffered { get; set; }

        public long Answered { get; set; }

        public long InboundAnswered { get; set; }

        public long Abandoned { get; set; }

        public long Failed { get; set; }

        public long Handled { get; set; }

        public long Transferred { get; set; }

        public long AnsweredVoice { get; set; }

        public long RecordedVoice { get; set; }

        public double TalkSeconds { get; set; }

        public double WrapUpSeconds { get; set; }

        public double AnswerSpeedSeconds { get; set; }

        public double AnswerRate => Total > 0 ? (double)Answered / Total : 0d;

        public double InboundAnswerRate => InboundOffered > 0 ? (double)InboundAnswered / InboundOffered : 0d;

        public double AbandonmentRate => InboundOffered > 0 ? (double)Abandoned / InboundOffered : 0d;

        public double AverageSpeedOfAnswerSeconds => InboundAnswered > 0 ? AnswerSpeedSeconds / InboundAnswered : 0d;

        public double AverageHandleTimeSeconds => Handled > 0 ? (TalkSeconds + WrapUpSeconds) / Handled : 0d;

        public double TransferRate => Answered > 0 ? (double)Transferred / Answered : 0d;

        public double RecordingCoverage => AnsweredVoice > 0 ? (double)RecordedVoice / AnsweredVoice : 0d;
    }

    internal sealed class QueueServiceLevelMetrics
    {
        public long EligibleOffered { get; set; }

        public long Answered { get; set; }

        public long ServiceLevelEligibleOffered { get; set; }

        public long AnsweredWithinThreshold { get; set; }

        public double AnswerSpeedSeconds { get; set; }

        public bool HasServiceLevel { get; set; }

        public double ServiceLevel => HasServiceLevel ? (double)AnsweredWithinThreshold / ServiceLevelEligibleOffered : 0d;

        public double AverageSpeedOfAnswerSeconds => Answered > 0 ? AnswerSpeedSeconds / Answered : 0d;
    }
}
