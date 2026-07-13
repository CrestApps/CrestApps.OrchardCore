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

internal sealed class EnterpriseInteractionReportProvider : IReport
{
    private readonly ISession _session;
    private readonly IActivityQueueManager _queueManager;
    private readonly EnterpriseInteractionReportDefinition _definition;
    private readonly IStringLocalizer _stringLocalizer;

    public EnterpriseInteractionReportProvider(
        ISession session,
        IActivityQueueManager queueManager,
        EnterpriseInteractionReportDefinition definition,
        IStringLocalizer stringLocalizer)
    {
        _session = session;
        _queueManager = queueManager;
        _definition = definition;
        _stringLocalizer = stringLocalizer;
    }

    public string Name => _definition.Name;

    public LocalizedString DisplayName => _definition.DisplayName();

    public LocalizedString Description => _definition.Description();

    public string Category => ReportsConstants.Categories.ContactCenter;

    public Permission Permission => ContactCenterPermissions.ViewReports;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var interactions = (await _session.Query<Interaction, InteractionIndex>(
            index => index.CreatedUtc >= context.FromUtc && index.CreatedUtc <= context.ToUtc,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken))
            .ToArray();
        var criteria = ContactCenterReportFilter.GetCriteria(context.Filter);
        var filteredInteractions = ContactCenterReportingService.FilterInteractions(interactions, criteria);

        var queues = (await _queueManager.GetAllAsync(cancellationToken))
            .ToDictionary(queue => queue.ItemId, StringComparer.Ordinal);

        return _definition.Kind switch
        {
            EnterpriseInteractionReportKind.ExecutiveSummary => BuildExecutiveSummary(filteredInteractions),
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
            _ => new ReportDocument(),
        };
    }

    private IStringLocalizer S => _stringLocalizer;

    private ReportDocument BuildExecutiveSummary(IReadOnlyList<Interaction> interactions)
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
            .Add(BuildPerformanceTable(
                S["Channel performance"].Value,
                S["Channel"].Value,
                interactions.GroupBy(interaction => interaction.Channel.ToString())));
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

        var rows = groups
            .Select(group => new { Label = group.Key, Metrics = Aggregate(group) })
            .OrderByDescending(entry => entry.Metrics.Total)
            .Select(entry => new ReportRow(
            [
                entry.Label,
                ReportFormat.Number(entry.Metrics.Total),
                ReportFormat.Number(entry.Metrics.Answered),
                ReportFormat.Number(entry.Metrics.Abandoned),
                ReportFormat.Number(entry.Metrics.Failed),
                ReportFormat.Percent(entry.Metrics.AnswerRate),
                ReportFormat.Percent(entry.Metrics.AbandonmentRate),
                ReportFormat.Duration(entry.Metrics.AverageSpeedOfAnswerSeconds),
                ReportFormat.Duration(entry.Metrics.AverageHandleTimeSeconds),
            ]));

        return ReportSection.ForTable(title, columns, rows);
    }

    private ReportDocument BuildInteractionDetail(IReadOnlyList<Interaction> interactions)
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
                DisplayOrUnknown(interaction.AgentId),
                DisplayOrUnknown(interaction.ProviderName),
                ReportFormat.Duration(GetWaitSeconds(interaction)),
                ReportFormat.Duration(GetTalkSeconds(interaction)),
                ReportFormat.Duration(GetWrapUpSeconds(interaction)),
                ReportFormat.Number(interaction.TransferHistory.Count),
            ]));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Interactions"].Value, columns, rows));
    }

    private ReportDocument BuildTransferAnalysis(IReadOnlyList<Interaction> interactions)
    {
        var transfers = interactions.SelectMany(interaction => interaction.TransferHistory);
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
            .Select(entry => entry.Row);

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Transfer outcomes"].Value, columns, rows));
    }

    private ReportDocument BuildRecordingCoverage(IReadOnlyList<Interaction> interactions)
    {
        var voice = interactions.Where(interaction => interaction.Channel == InteractionChannel.Voice && interaction.AnsweredUtc.HasValue);

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
            .Select(entry => entry.Row);

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
            .Select(entry => entry.Row);

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

        var rows = interactions
            .Where(IsInboundOffered)
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
            .Select(entry => entry.Row);

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

        var rows = interactions
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
                            group.Key,
                            ReportFormat.Number(count),
                            ReportFormat.Number(completed),
                            ReportFormat.Percent(count > 0 ? (double)completed / count : 0d),
                            ReportFormat.Duration(average),
                            ReportFormat.Duration(wrapUp),
                        ])
                        : new ReportRow(
                        [
                            group.Key,
                            ReportFormat.Number(count),
                            ReportFormat.Duration(count > 0 ? talk / count : 0d),
                            ReportFormat.Duration(count > 0 ? wrapUp / count : 0d),
                            ReportFormat.Duration(average),
                            ReportFormat.Duration(talk + wrapUp),
                        ]),
                };
            })
            .OrderByDescending(entry => entry.Average)
            .Select(entry => entry.Row);

        return ReportSection.ForTable(title, columns, rows);
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

                if (thresholdSeconds > 0 && GetWaitSeconds(interaction) <= thresholdSeconds)
                {
                    metrics.AnsweredWithinThreshold++;
                }
            }
            else if (IsAbandoned(interaction))
            {
                metrics.EligibleOffered++;
            }
        }

        metrics.HasServiceLevel = thresholdSeconds > 0 && metrics.EligibleOffered > 0;

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

        public long AnsweredWithinThreshold { get; set; }

        public double AnswerSpeedSeconds { get; set; }

        public bool HasServiceLevel { get; set; }

        public double ServiceLevel => HasServiceLevel ? (double)AnsweredWithinThreshold / EligibleOffered : 0d;

        public double AverageSpeedOfAnswerSeconds => Answered > 0 ? AnswerSpeedSeconds / Answered : 0d;
    }
}

internal sealed record EnterpriseInteractionReportDefinition(
    string Name,
    Func<LocalizedString> DisplayName,
    Func<LocalizedString> Description,
    EnterpriseInteractionReportKind Kind);

internal enum EnterpriseInteractionReportKind
{
    ExecutiveSummary,
    VolumeTrend,
    IntervalPerformance,
    ChannelPerformance,
    DirectionPerformance,
    ProviderPerformance,
    OutcomePerformance,
    InteractionDetail,
    TransferAnalysis,
    RecordingCoverage,
    QueueServiceLevel,
    QueueAbandonment,
    AgentHandleTime,
    WrapUpPerformance,
}
