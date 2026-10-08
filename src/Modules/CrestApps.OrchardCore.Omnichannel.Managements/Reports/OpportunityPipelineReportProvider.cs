using System.Globalization;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// The opportunity pipeline: the open deals by stage with their amount and their amount weighted by the chance of
/// winning, and the deals closed in the period with the share that was won.
/// </summary>
public sealed class OpportunityPipelineReportProvider : OmnichannelReportBase, IReportFilterMetadata
{
    private readonly ISession _session;
    private readonly INamedCatalog<OpportunityStage> _stages;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityPipelineReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="stages">The opportunity stage catalog.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityPipelineReportProvider(
        ISession session,
        INamedCatalog<OpportunityStage> stages,
        IStringLocalizer<OpportunityPipelineReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
        _stages = stages;
    }

    /// <inheritdoc/>
    /// <remarks>The report reads leads and opportunities, not activities, so only the period applies.</remarks>
    public IReadOnlyCollection<string> FilterNames => [];

    /// <inheritdoc/>
    public override string Name => "omnichannel-opportunity-pipeline";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Opportunity pipeline"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Open opportunities by stage with their amount and weighted amount, and the win rate of the opportunities closed in the period."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc.GetValueOrDefault();
        var to = range.ToUtc.GetValueOrDefault(DateTime.MaxValue);

        var stages = (await _stages.GetAllAsync(cancellationToken)).ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);

        var open = (await _session.QueryIndex<OpportunityIndex>(index => index.Latest && !index.IsClosed)
            .ListAsync(cancellationToken))
            .ToList();

        // Deals are placed in the period by their close date: the date a deal is expected to close, or closed on.
        var closed = (await _session.QueryIndex<OpportunityIndex>(index =>
                index.Latest &&
                index.IsClosed &&
                index.CloseDate >= from &&
                index.CloseDate <= to)
            .ListAsync(cancellationToken))
            .ToList();

        var pipelineColumns = new[]
        {
            new ReportColumn(S["Stage"].Value),
            new ReportColumn(S["Opportunities"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Amount"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Weighted amount"].Value, ReportColumnAlign.End),
        };

        var pipelineRows = open
            .GroupBy(opportunity => opportunity.StageId ?? string.Empty)
            .Select(group => (
                Name: stages.TryGetValue(group.Key, out var stage) ? stage.Name : S["(No stage)"].Value,
                Order: stages.TryGetValue(group.Key, out var ordered) ? ordered.Order : int.MaxValue,
                Count: group.Count(),
                Amount: group.Sum(opportunity => opportunity.Amount ?? 0),
                Weighted: group.Sum(opportunity => (opportunity.Amount ?? 0) * (opportunity.Probability ?? 0) / 100m)))
            .OrderBy(entry => entry.Order)
            .Select(entry => new ReportRow(
            [
                entry.Name,
                ReportFormat.Number(entry.Count),
                Money(entry.Amount),
                Money(entry.Weighted),
            ]))
            .ToList();

        pipelineRows.Add(new ReportRow(
        [
            S["Open pipeline"].Value,
            ReportFormat.Number(open.Count),
            Money(open.Sum(opportunity => opportunity.Amount ?? 0)),
            Money(open.Sum(opportunity => (opportunity.Amount ?? 0) * (opportunity.Probability ?? 0) / 100m)),
        ], emphasize: true));

        var won = closed.Where(opportunity => opportunity.IsWon).ToList();

        var closedColumns = new[]
        {
            new ReportColumn(S["Measure"].Value),
            new ReportColumn(S["Value"].Value, ReportColumnAlign.End),
        };

        var closedRows = new List<ReportRow>
        {
            new([S["Closed in the period"].Value, ReportFormat.Number(closed.Count)]),
            new([S["Won"].Value, ReportFormat.Number(won.Count)]),
            new([S["Lost"].Value, ReportFormat.Number(closed.Count - won.Count)]),
            new([S["Win rate"].Value, ReportFormat.Percent(closed.Count > 0 ? (double)won.Count / closed.Count : 0)]),
            new([S["Amount won"].Value, Money(won.Sum(opportunity => opportunity.Amount ?? 0))]),
        };

        return new ReportDocument()
            .Add(ReportSection.ForTable(S["Open pipeline by stage"].Value, pipelineColumns, pipelineRows))
            .Add(ReportSection.ForTable(S["Closed opportunities"].Value, closedColumns, closedRows));
    }

    private static string Money(decimal value)
        => value.ToString("N2", CultureInfo.CurrentCulture);
}
