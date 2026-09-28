using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The AI handoff / containment report: of the automated conversations that concluded in the period, how many
/// the bot handled to completion versus escalated to a human, plus the average time to escalate. It measures the
/// terminal reason recorded on the automated activity, so it captures SMS handoffs and after-hours callbacks.
/// </summary>
public sealed class HandoffContainmentReportProvider : OmnichannelReportBase
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandoffContainmentReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public HandoffContainmentReportProvider(
        ISession session,
        IStringLocalizer<HandoffContainmentReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public override string Name => "omnichannel-ai-handoff-containment";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["AI handoff & containment"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Of the automated conversations concluded in the period, how many the AI contained versus escalated to a live agent."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc.GetValueOrDefault();
        var to = range.ToUtc.GetValueOrDefault();

        // The population is: automated conversations the AI contained, plus every conversation that escalated to a
        // human. A routed voice handoff leaves the automated lane (it becomes an agent call), so escalations are
        // matched on the durable AiEscalated flag rather than on Source. Two queries keep each side an indexed
        // lookup; they never overlap (contained is AiEscalated == false).
        var contained = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
            index => index.Status == ActivityStatus.Completed &&
                     index.Source == ActivitySources.Automatic &&
                     !index.AiEscalated &&
                     index.CompletedUtc >= from &&
                     index.CompletedUtc <= to,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var escalated = await _session.Query<OmnichannelActivity, OmnichannelActivityIndex>(
            index => index.Status == ActivityStatus.Completed &&
                     index.AiEscalated &&
                     index.CompletedUtc >= from &&
                     index.CompletedUtc <= to,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var summary = HandoffContainmentAggregator.Compute([.. contained, .. escalated]);

        var overviewColumns = new[]
        {
            new ReportColumn(S["Metric"].Value),
            new ReportColumn(S["Value"].Value, ReportColumnAlign.End),
        };

        var overviewRows = new List<ReportRow>
        {
            new([S["Automated conversations"].Value, ReportFormat.Number(summary.Total)]),
            new([S["Contained by AI"].Value, ReportFormat.Number(summary.Contained)]),
            new([S["Escalated to an agent"].Value, ReportFormat.Number(summary.Escalated)]),
            new([S["Containment rate"].Value, ReportFormat.Percent(summary.ContainmentRate)], emphasize: true),
            new([S["Escalation rate"].Value, ReportFormat.Percent(summary.EscalationRate)]),
            new([S["Average time to escalate"].Value, FormatDuration(summary.AverageTimeToHandoff)]),
        };

        var document = new ReportDocument()
            .Add(ReportSection.ForTable(S["Containment overview"].Value, overviewColumns, overviewRows));

        if (summary.EscalatedByReason.Count > 0)
        {
            var reasonColumns = new[]
            {
                new ReportColumn(S["Escalation reason"].Value),
                new ReportColumn(S["Count"].Value, ReportColumnAlign.End),
            };

            var reasonRows = summary.EscalatedByReason
                .OrderByDescending(entry => entry.Value)
                .Select(entry => new ReportRow([ResolveReason(entry.Key), ReportFormat.Number(entry.Value)]))
                .ToList();

            document.Add(ReportSection.ForTable(S["Escalations by reason"].Value, reasonColumns, reasonRows));
        }

        return document;
    }

    private string ResolveReason(string reasonKey) => reasonKey switch
    {
        OmnichannelConstants.TerminalReasons.HandedOffToAgent => S["Transferred to a live agent"].Value,
        OmnichannelConstants.TerminalReasons.HandedOffAfterHoursCallback => S["After-hours callback scheduled"].Value,
        HandoffContainmentAggregator.RoutedVoiceReason => S["Transferred to a live agent (voice)"].Value,
        _ => reasonKey,
    };

    private string FormatDuration(TimeSpan? duration)
        => duration is { } value
            ? S["{0} min", Math.Round(value.TotalMinutes, 1)].Value
            : S["n/a"].Value;
}
