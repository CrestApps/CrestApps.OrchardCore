using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Agreements.Reports;

/// <summary>
/// The hours left of an agreement each day of its period against an even pace: the chosen agreement with its days, or
/// the agreements that used the most of their hours.
/// </summary>
internal sealed class AgreementBurnDownReport : IReport
{
    // How many agreements are drawn when none is chosen.
    private const int MostUsed = 6;

    private readonly AgreementReportData _data;

    internal readonly IStringLocalizer S;

    public AgreementBurnDownReport(AgreementReportData data, IStringLocalizer<AgreementBurnDownReport> stringLocalizer)
    {
        _data = data;
        S = stringLocalizer;
    }

    public static string Name => "agreements-burn-down";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AgreementBurnDownReport>("Agreement Burn-down");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<AgreementBurnDownReport>("The hours left of an agreement each day of its period, up to the last day chosen, against an even pace through the period.");

    public static string Category => ReportsConstants.Categories.BillingUsage;

    public static Permission Permission => AgreementsPermissions.ViewAgreements;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.Today;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = await _data.ReadAsync(context, cancellationToken);
        var document = new ReportDocument();

        if (items.Count == 0)
        {
            var empty = ReportSection.ForTable(S["Agreements"].Value, [], []);
            empty.EmptyMessage = S["No agreement with covered hours is in force on these days."].Value;

            return document.Add(empty);
        }

        var chosen = !string.IsNullOrEmpty(context.Filter.GetOrDefault<string>(AgreementReportData.AgreementFilterKey));
        var shown = chosen ? items : [.. items.OrderByDescending(item => item.Balance.UsedShare).ThenBy(item => item.Agreement.Number, StringComparer.Ordinal).Take(MostUsed)];

        foreach (var item in shown)
        {
            var days = AgreementBurnDown.Days(item.Agreement, item.Balance, item.Through);
            var chart = new ReportChart
            {
                Type = ReportChartType.Line,
                Labels = [.. days.Select(day => AgreementUsageReport.Day(day.Day))],
                Datasets = [new ReportChartDataset(S["Hours left"].Value, days.Select(day => Hours(day.LeftMinutes)))],
            };

            if (item.Balance.PeriodEnd is not null)
            {
                chart.Datasets.Add(new ReportChartDataset(S["Even pace"].Value, days.Select(day => Hours(day.EvenLeftMinutes ?? 0))));
            }

            var section = ReportSection.ForChart(item.Title, chart, chosen ? 12 : 6);
            section.Description = S["{0} of {1} used, {2} left.", AgreementUsageReport.Time(item.Balance.UsedMinutes), AgreementUsageReport.Time(item.Balance.CoveredMinutes), AgreementUsageReport.Time(item.Balance.LeftMinutes)].Value;
            document.Add(section);

            if (chosen)
            {
                var table = ReportSection.ForTable(S["Days"].Value,
                    [
                        new ReportColumn(S["Day"].Value),
                        new ReportColumn(S["Used to date"].Value, ReportColumnAlign.End),
                        new ReportColumn(S["Hours left"].Value, ReportColumnAlign.End),
                        new ReportColumn(S["Even pace"].Value, ReportColumnAlign.End),
                    ],
                    [.. days.Select(day => new ReportRow(
                    [
                        AgreementUsageReport.Day(day.Day),
                        AgreementUsageReport.Time(day.UsedMinutes),
                        AgreementUsageReport.Time(day.LeftMinutes),
                        day.EvenLeftMinutes is { } even ? AgreementUsageReport.Time(even) : "-",
                    ]))]);
                document.Add(table);
            }
        }

        if (!chosen && items.Count > shown.Count)
        {
            var more = ReportSection.ForTable(S["More agreements"].Value, [], []);
            more.EmptyMessage = S["The {0} agreements that used the most of their hours are shown. Choose an agreement to see its days.", MostUsed].Value;
            document.Add(more);
        }

        return document;
    }

    private static double Hours(double minutes)
        => Math.Round(minutes / 60, 2);
}
