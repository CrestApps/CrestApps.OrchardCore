using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// Lead conversion by source and by list: how many leads each produced in the period, how many were converted, and
/// how long conversion took, so the lists and sources that turn into customers can be told from those that do not.
/// </summary>
public sealed class LeadConversionReportProvider : OmnichannelReportBase, IReportFilterMetadata
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadConversionReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadConversionReportProvider(
        ISession session,
        IStringLocalizer<LeadConversionReportProvider> stringLocalizer)
        : base(stringLocalizer)
    {
        _session = session;
    }

    /// <inheritdoc/>
    /// <remarks>The report reads leads and opportunities, not activities, so only the period applies.</remarks>
    public IReadOnlyCollection<string> FilterNames => [];

    /// <inheritdoc/>
    public override string Name => "omnichannel-lead-conversion";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Lead conversion by source and list"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Leads created in the period by source and by list, with how many were converted and how long it took."];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var from = range.FromUtc.GetValueOrDefault();
        var to = range.ToUtc.GetValueOrDefault(DateTime.MaxValue);

        var leads = (await _session.QueryIndex<LeadIndex>(index =>
                index.Latest &&
                index.CreatedUtc >= from &&
                index.CreatedUtc <= to)
            .ListAsync(cancellationToken))
            .ToList();

        return new ReportDocument()
            .Add(Section(S["By source"].Value, S["Source"].Value, leads, lead => lead.Source))
            .Add(Section(S["By list"].Value, S["List"].Value, leads, lead => lead.ListName));
    }

    private ReportSection Section(string title, string keyName, List<LeadIndex> leads, Func<LeadIndex, string> key)
    {
        var columns = new[]
        {
            new ReportColumn(keyName),
            new ReportColumn(S["Leads"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Converted"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Conversion rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Average time to convert"].Value, ReportColumnAlign.End),
        };

        var none = S["(None)"].Value;

        var rows = leads
            .GroupBy(lead => string.IsNullOrWhiteSpace(key(lead)) ? none : key(lead))
            .Select(group =>
            {
                var total = group.Count();
                var converted = group.Where(lead => lead.IsConverted).ToList();
                var durations = converted
                    .Where(lead => lead.ConvertedUtc.HasValue && lead.CreatedUtc.HasValue)
                    .Select(lead => (lead.ConvertedUtc.Value - lead.CreatedUtc.Value).TotalSeconds)
                    .ToList();

                return (Name: group.Key, Total: total, Converted: converted.Count, AverageSeconds: durations.Count > 0 ? durations.Average() : (double?)null);
            })
            .OrderByDescending(entry => entry.Total)
            .Select(entry => new ReportRow(
            [
                entry.Name,
                ReportFormat.Number(entry.Total),
                ReportFormat.Number(entry.Converted),
                ReportFormat.Percent(entry.Total > 0 ? (double)entry.Converted / entry.Total : 0),
                entry.AverageSeconds.HasValue ? ReportFormat.Duration(entry.AverageSeconds.Value) : string.Empty,
            ]))
            .ToList();

        return ReportSection.ForTable(title, columns, rows);
    }
}
