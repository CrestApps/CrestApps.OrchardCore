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
using static CrestApps.OrchardCore.ContactCenter.Reports.Services.InteractionMetricsCalculator;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

internal sealed class EnterpriseInteractionReportProvider : IReport, IReportFilterMetadata, IContactCenterCapabilityDependentReport
{
    private readonly ISession _session;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly EnterpriseInteractionReportDefinition _definition;
    private readonly IContactCenterReportCapabilityGuard _capabilityGuard;
    private readonly IStringLocalizer _stringLocalizer;
    private readonly TimeSpan _maximumReportRange;
    private Dictionary<string, string> _agentUserNames = [];
    private HashSet<string> _absentFeatureIds = [];

    private static readonly string[] _executiveMetricRequirements =
        [null, null, null, null, null, null, null, ContactCenterConstants.Feature.Voice, ContactCenterConstants.Feature.Recording];

    private static readonly string[] _interactionDetailRequirements =
        [null, null, null, null, null, null, null, ContactCenterConstants.Feature.Voice, null, null, null, ContactCenterConstants.Feature.Voice];

    private static readonly string[] _agentPerformanceRequirements =
    [
        null,
        null,
        null,
        null,
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Recording,
        ContactCenterConstants.Feature.Recording,
        null,
    ];

    private static readonly string[] _usageRequirements =
    [
        null,
        null,
        null,
        null,
        null,
        null,
        ContactCenterConstants.Feature.Voice,
        ContactCenterConstants.Feature.Recording,
    ];

    public EnterpriseInteractionReportProvider(
        ISession session,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        EnterpriseInteractionReportDefinition definition,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer stringLocalizer,
        TimeSpan maximumReportRange)
    {
        _session = session;
        _queueManager = queueManager;
        _agentManager = agentManager;
        _definition = definition;
        _capabilityGuard = capabilityGuard;
        _stringLocalizer = stringLocalizer;
        _maximumReportRange = maximumReportRange;
    }

    public string Name => _definition.Name;

    public LocalizedString DisplayName => _definition.DisplayName();

    public LocalizedString Description => _definition.Description();

    public string Category => _definition.Category;

    public Permission Permission => ContactCenterPermissions.ViewReports;

    public IReadOnlyCollection<string> FilterNames => _definition.FilterNames;

    public IReadOnlyCollection<string> RequiredFeatureIds => ContactCenterReportCapabilityRequirements.For(_definition.Kind);

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var missingFeatures = await _capabilityGuard.GetMissingFeaturesAsync(RequiredFeatureIds, cancellationToken);

        if (missingFeatures.Count > 0)
        {
            return _capabilityGuard.DescribeUnavailable(missingFeatures);
        }

        // Reports whose subject matter is only partly produced by a capability are not withheld: their primary
        // figures are real. What they must not do is publish the capability's columns as zeroes alongside them, so
        // the columns those capabilities feed are dropped instead of rendered empty.
        _absentFeatureIds = [.. await _capabilityGuard.GetMissingFeaturesAsync(
            [ContactCenterConstants.Feature.Voice, ContactCenterConstants.Feature.Recording],
            cancellationToken)];

        ContactCenterReportingService.EnsureRangeWithinLimit(context.FromUtc, context.ToUtc, _maximumReportRange);

        var interactions = (await _session.Query<Interaction, InteractionIndex>(
            index => index.CreatedUtc >= context.FromUtc && index.CreatedUtc <= context.ToUtc,
            collection: ContactCenterStorage.CollectionName)
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
            EnterpriseInteractionReportKind.LongInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => GetTalkSeconds(interaction) >= 900d), S["Long interactions (15+ minutes)"].Value),
            EnterpriseInteractionReportKind.FailedInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => interaction.Status == InteractionStatus.Failed), S["Failed interactions"].Value),
            EnterpriseInteractionReportKind.AbandonedInteractionDetail => BuildExceptionDetail(filteredInteractions.Where(IsAbandoned), S["Abandoned interactions"].Value),
            EnterpriseInteractionReportKind.HighWaitDetail => BuildExceptionDetail(filteredInteractions.Where(interaction => GetWaitSeconds(interaction) >= 60d), S["High-wait interactions (60+ seconds)"].Value),
            EnterpriseInteractionReportKind.LifecycleDuration => BuildLifecycleDuration(filteredInteractions),
            EnterpriseInteractionReportKind.CallLegPerformance => await BuildCallLegPerformanceAsync(filteredInteractions, context, cancellationToken),
            _ => new ReportDocument(),
        };
    }

    private bool IsAvailable(string requiredFeatureId)
        => requiredFeatureId is null || !_absentFeatureIds.Contains(requiredFeatureId);

    /// <summary>
    /// Keeps the entries of a fixed layout whose producing capability is enabled. The same requirement list drives
    /// the columns and every row, so a column and its cells cannot drift apart.
    /// </summary>
    private T[] SelectAvailable<T>(IReadOnlyList<T> entries, IReadOnlyList<string> requirements)
    {
        if (entries.Count != requirements.Count)
        {
            throw new InvalidOperationException(
                $"A report layout declares {entries.Count} entries but {requirements.Count} capability requirements. " +
                "The requirement list must have one entry per column so a column and its cells cannot drift apart.");
        }

        if (_absentFeatureIds.Count == 0)
        {
            return [.. entries];
        }

        return [.. entries.Where((_, position) => IsAvailable(requirements[position]))];
    }

    private ReportRow SelectAvailableRow(
        IReadOnlyList<string> cells,
        IReadOnlyList<string> requirements,
        ReportRowKind kind = ReportRowKind.Detail)
        => new(SelectAvailable(cells, requirements), kind);

    private IStringLocalizer S => _stringLocalizer;

    private ReportDocument BuildExecutiveSummary(
        IReadOnlyList<Interaction> interactions,
        Dictionary<string, ActivityQueue> queues)
    {
        var totals = Aggregate(interactions);

        ReportMetric[] metrics =
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
        ];

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Executive performance"].Value, SelectAvailable(metrics, _executiveMetricRequirements)))
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
            .Select(interaction => SelectAvailableRow(
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
            ], _interactionDetailRequirements));

        return new ReportDocument()
            .Add(ReportSection.ForTable(title ?? S["Interactions"].Value, SelectAvailable(columns, _interactionDetailRequirements), rows));
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
                    Row = SelectAvailableRow(
                    [
                        ResolveAgentName(group.Key),
                        ReportFormat.Number(metrics.Total),
                        ReportFormat.Number(metrics.Answered),
                        ReportFormat.Number(metrics.Failed),
                        ReportFormat.Number(transfers),
                        ReportFormat.Number(recorded),
                        ReportFormat.Percent(answeredVoice > 0 ? (double)recorded / answeredVoice : 0d),
                        ReportFormat.Duration(metrics.AverageHandleTimeSeconds),
                    ], _agentPerformanceRequirements),
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

        rows.Add(SelectAvailableRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(totals.Total),
            ReportFormat.Number(totals.Answered),
            ReportFormat.Number(totals.Failed),
            ReportFormat.Number(totalTransfers),
            ReportFormat.Number(totalRecorded),
            ReportFormat.Percent(totalAnsweredVoice > 0 ? (double)totalRecorded / totalAnsweredVoice : 0d),
            ReportFormat.Duration(totals.AverageHandleTimeSeconds),
        ], _agentPerformanceRequirements, ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Agent performance"].Value, SelectAvailable(columns, _agentPerformanceRequirements), rows));
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
                    Row = SelectAvailableRow(
                    [
                        group.Key,
                        ReportFormat.Number(group.LongCount()),
                        ReportFormat.Number(group.LongCount(interaction => interaction.AnsweredUtc.HasValue)),
                        ReportFormat.Duration(connectedSeconds),
                        ReportFormat.Duration(group.Sum(GetWrapUpSeconds)),
                        ReportFormat.Duration(group.Sum(GetWaitSeconds)),
                        ReportFormat.Number(group.Sum(interaction => interaction.TransferHistory.Count)),
                        ReportFormat.Number(group.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference))),
                    ], _usageRequirements),
                };
            })
            .OrderByDescending(entry => entry.ConnectedSeconds)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(SelectAvailableRow(
        [
            S["Grand total"].Value,
            ReportFormat.Number(interactions.Count),
            ReportFormat.Number(interactions.LongCount(interaction => interaction.AnsweredUtc.HasValue)),
            ReportFormat.Duration(interactions.Sum(GetTalkSeconds)),
            ReportFormat.Duration(interactions.Sum(GetWrapUpSeconds)),
            ReportFormat.Duration(interactions.Sum(GetWaitSeconds)),
            ReportFormat.Number(interactions.Sum(interaction => interaction.TransferHistory.Count)),
            ReportFormat.Number(interactions.LongCount(interaction => !string.IsNullOrEmpty(interaction.RecordingReference))),
        ], _usageRequirements, ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Usage summary"].Value, SelectAvailable(columns, _usageRequirements), rows));
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

    private async Task<ReportDocument> BuildCallLegPerformanceAsync(
        IReadOnlyList<Interaction> interactions,
        ReportContext context,
        CancellationToken cancellationToken)
    {
        var interactionIds = interactions
            .Select(interaction => interaction.ItemId)
            .Where(itemId => !string.IsNullOrEmpty(itemId))
            .ToHashSet(StringComparer.Ordinal);

        // Legs belong to the call session, which the voice topology projector owns. The report reads them from
        // there rather than from the interaction, because the interaction never carried them.
        var callSessions = interactionIds.Count == 0
            ? []
            : (await _session.Query<CallSession, CallSessionIndex>(
                index => index.CreatedUtc >= context.FromUtc && index.CreatedUtc <= context.ToUtc,
                collection: ContactCenterStorage.CollectionName)
                .ListAsync(cancellationToken))
                .Where(callSession => interactionIds.Contains(callSession.InteractionId))
                .ToArray();

        var legs = callSessions.SelectMany(callSession => callSession.Legs).ToArray();
        var columns = new[]
        {
            new ReportColumn(S["Leg status"].Value),
            new ReportColumn(S["Legs"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average duration"].Value, ReportColumnAlign.End),
        };
        var rows = legs
            .GroupBy(leg => leg.Status.ToString(), StringComparer.Ordinal)
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
}
