using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Builds the queue abandonment report: per queue, the inbound calls it was offered and how each one ended.
/// </summary>
/// <remarks>
/// A caller who hung up before an agent answered abandoned, and the wait before abandoning runs from joining the queue
/// to hanging up. A caller the platform sent to voicemail did not abandon: they are counted in their own column, with
/// the wait from joining the queue to leaving it for voicemail, and are left out of the abandonment rate's numerator.
/// Every call offered counts toward its denominator.
/// </remarks>
internal static class QueueAbandonmentReport
{
    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="interactions">The inbound queue interactions of the period.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <param name="queueName">Resolves a queue identifier to the name the report shows.</param>
    /// <param name="S">The localizer for the report's labels.</param>
    /// <returns>The report.</returns>
    public static ReportDocument Build(
        IReadOnlyList<Interaction> interactions,
        InteractionOutcomeClassifier outcomes,
        Func<string, string> queueName,
        IStringLocalizer S)
    {
        var columns = new[]
        {
            new ReportColumn(S["Queue"].Value),
            new ReportColumn(S["Offered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Answered"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandoned"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Abandonment rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg wait before abandon"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Voicemail"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg wait before voicemail"].Value, ReportColumnAlign.End),
        };

        var offered = interactions.Where(InteractionMetricsCalculator.IsInboundOffered).ToArray();
        var rows = offered
            .GroupBy(interaction => interaction.QueueId ?? string.Empty, StringComparer.Ordinal)
            .Select(group => Measure([.. group], outcomes))
            .Select(measure => (measure.AbandonmentRate, Row: new ReportRow(Cells(queueName(measure.QueueId), measure))))
            .OrderByDescending(entry => entry.AbandonmentRate)
            .Select(entry => entry.Row)
            .ToList();

        rows.Add(new ReportRow(Cells(S["Grand total"].Value, Measure(offered, outcomes)), ReportRowKind.GrandTotal));

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Queue abandonment"].Value, columns, rows));
    }

    private static string[] Cells(string label, QueueMeasure measure)
        =>
        [
            label,
            ReportFormat.Number(measure.Offered),
            ReportFormat.Number(measure.Answered),
            ReportFormat.Number(measure.Abandoned.Length),
            ReportFormat.Percent(measure.AbandonmentRate),
            ReportFormat.Duration(measure.Abandoned.Length > 0 ? measure.Abandoned.Average(measure.Outcomes.GetWaitBeforeAbandonSeconds) : 0d),
            ReportFormat.Number(measure.Voicemail.Length),
            ReportFormat.Duration(measure.Voicemail.Length > 0 ? measure.Voicemail.Average(measure.Outcomes.GetWaitBeforeVoicemailSeconds) : 0d),
        ];

    private static QueueMeasure Measure(Interaction[] interactions, InteractionOutcomeClassifier outcomes)
        => new(
            interactions.Select(interaction => interaction.QueueId).FirstOrDefault() ?? string.Empty,
            interactions.LongLength,
            interactions.LongCount(outcomes.IsAnswered),
            [.. interactions.Where(outcomes.IsAbandoned)],
            [.. interactions.Where(outcomes.IsVoicemail)],
            outcomes);

    private sealed record QueueMeasure(
        string QueueId,
        long Offered,
        long Answered,
        Interaction[] Abandoned,
        Interaction[] Voicemail,
        InteractionOutcomeClassifier Outcomes)
    {
        public double AbandonmentRate => Offered > 0 ? (double)Abandoned.Length / Offered : 0d;
    }
}
