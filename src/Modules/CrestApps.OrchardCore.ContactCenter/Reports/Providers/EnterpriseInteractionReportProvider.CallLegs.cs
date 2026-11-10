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
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;
using static CrestApps.OrchardCore.ContactCenter.Reports.Services.InteractionMetricsCalculator;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Providers;

/// <summary>
/// The call leg performance report: how each leg of a call was handled.
/// </summary>
internal sealed partial class EnterpriseInteractionReportProvider
{
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
        var range = context.Filter.GetDateRange();
        var fromUtc = range.FromUtc.GetValueOrDefault();
        var toUtc = range.ToUtc.GetValueOrDefault();
        var callSessions = interactionIds.Count == 0
            ? []
            : (await _session.Query<CallSession, CallSessionIndex>(
                index => index.CreatedUtc >= fromUtc && index.CreatedUtc <= toUtc,
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
        => ContactCenterReportNames.Queue(queueId, queues, S);

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
}
