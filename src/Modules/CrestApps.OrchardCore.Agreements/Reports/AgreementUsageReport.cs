using System.Globalization;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Agreements.Reports;

/// <summary>
/// How the hours of agreements stand in the period that holds the report's last day: covered, used, left and past what
/// is covered, the share used, and when they ran out or are expected to at the pace so far; and where the time came
/// from (tickets, work orders...).
/// </summary>
internal sealed class AgreementUsageReport : IReport
{
    // A share used from which an agreement is shown as close to its hours, and the colors of the share.
    private const double WarningShare = 0.8;
    private static readonly ReportStyle _warning = ReportStyle.Create("#92400E", "#FEF3C7");
    private static readonly ReportStyle _over = ReportStyle.Create("#991B1B", "#FEE2E2", bold: true);

    private readonly AgreementReportData _data;

    internal readonly IStringLocalizer S;

    public AgreementUsageReport(AgreementReportData data, IStringLocalizer<AgreementUsageReport> stringLocalizer)
    {
        _data = data;
        S = stringLocalizer;
    }

    public static string Name => "agreements-usage";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AgreementUsageReport>("Agreement Usage");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<AgreementUsageReport>("For each agreement with covered hours, as of the last day chosen: the hours covered, used and left in its period, the overage, and when the hours ran out or are expected to.");

    public static string Category => ReportsConstants.Categories.BillingUsage;

    public static Permission Permission => AgreementsPermissions.ViewAgreements;

    public static ReportDefaultDateRange DefaultDateRange { get; } = ReportDefaultDateRange.Today;

    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var items = await _data.ReadAsync(context, cancellationToken);
        var covered = items.Sum(item => item.Balance.CoveredMinutes);
        var used = items.Sum(item => item.Balance.UsedMinutes);

        var document = new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Agreements"].Value, ReportFormat.Number(items.Count)),
                new ReportMetric(S["Hours covered"].Value, Time(covered)),
                new ReportMetric(S["Hours used"].Value, Time(used)),
                new ReportMetric(S["Overage"].Value, Time(items.Sum(item => item.Balance.OverageMinutes)), S["Time past what the agreements cover, which is billable."].Value),
                new ReportMetric(S["Past 80%"].Value, ReportFormat.Number(items.Count(item => item.Balance.UsedShare >= WarningShare && item.Balance.UsedShare < 1))),
                new ReportMetric(S["Used up"].Value, ReportFormat.Number(items.Count(item => item.Balance.UsedShare >= 1))),
            ]));

        var accounts = await _data.GetAccountNamesAsync(items.Select(item => item.Agreement.AccountId));
        var rows = items
            .OrderByDescending(item => item.Balance.UsedShare)
            .ThenBy(item => item.Agreement.Number, StringComparer.Ordinal)
            .Select(item =>
            {
                var balance = item.Balance;
                var row = new ReportRow(
                [
                    item.Title,
                    accounts.GetValueOrDefault(item.Agreement.AccountId ?? string.Empty) ?? item.Agreement.AccountId ?? string.Empty,
                    Period(balance.PeriodStart, balance.PeriodEnd),
                    Time(balance.CoveredMinutes),
                    Time(balance.UsedMinutes),
                    Time(balance.LeftMinutes),
                    Time(balance.OverageMinutes),
                    ReportFormat.Percent(balance.UsedShare),
                    RunOut(AgreementBurnDown.ProjectRunOut(item.Agreement, balance, item.Through)),
                ]);

                return balance.UsedShare >= 1
                    ? row.WithCellStyle(7, _over)
                    : balance.UsedShare >= WarningShare ? row.WithCellStyle(7, _warning) : row;
            })
            .ToList();

        var table = ReportSection.ForTable(S["Agreements"].Value,
            [
                new ReportColumn(S["Agreement"].Value),
                new ReportColumn(S["Account"].Value),
                new ReportColumn(S["Period"].Value),
                new ReportColumn(S["Covered"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Used"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Left"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Overage"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Used share"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Hours run out"].Value),
            ],
            rows);
        table.Description = S["The period is the whole agreement for a block of hours, and the month for an allowance. The run-out day follows the pace from the period's start; agreements that used the most come first."].Value;
        table.EmptyMessage = S["No agreement with covered hours is in force on these days."].Value;
        document.Add(table);

        var sources = items
            .SelectMany(item => item.Balance.Entries)
            .GroupBy(entry => entry.Usage.Source ?? string.Empty, StringComparer.Ordinal)
            .Select(group => (Source: group.Key, Minutes: group.Sum(entry => entry.RoundedMinutes), People: group.Select(entry => entry.Usage.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count()))
            .OrderByDescending(row => row.Minutes)
            .Select(row => new ReportRow(
            [
                string.IsNullOrEmpty(row.Source) ? S["Other"].Value : row.Source,
                Time(row.Minutes),
                used <= 0 ? "-" : ReportFormat.Percent(row.Minutes / used),
                ReportFormat.Number(row.People),
            ]))
            .ToList();

        var bySource = ReportSection.ForTable(S["Where the hours went"].Value,
            [
                new ReportColumn(S["Source"].Value),
                new ReportColumn(S["Used"].Value, ReportColumnAlign.End),
                new ReportColumn(S["Share"].Value, ReportColumnAlign.End),
                new ReportColumn(S["People"].Value, ReportColumnAlign.End),
            ],
            sources);
        bySource.EmptyMessage = S["No time was used under these agreements in their periods."].Value;

        return document.Add(bySource);
    }

    private string RunOut(AgreementRunOut runOut)
        => runOut switch
        {
            { Reached: true, Day: { } day } => S["Ran out on {0}", Day(day)].Value,
            { Reached: true } => S["Ran out"].Value,
            { Day: { } day } => S["Expected on {0}", Day(day)].Value,
            _ => "-",
        };

    private string Period(DateOnly start, DateOnly? end)
        => end is { } last ? S["{0} to {1}", Day(start), Day(last)].Value : S["From {0}", Day(start)].Value;

    internal static string Day(DateOnly day)
        => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string Time(double minutes)
        => ReportFormat.Duration(Math.Round(minutes) * 60);
}
