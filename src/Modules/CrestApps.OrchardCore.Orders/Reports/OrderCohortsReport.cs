using System.Globalization;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Reports;

/// <summary>
/// Retention by cohort: the buyers whose first paid order was in each month of the period, and the share of them who
/// bought again in each month after.
/// </summary>
public sealed class OrderCohortsReport : OrderReportBase, IReport
{
    public const int MonthsAfter = 6;

    private static readonly DateTime _beginning = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public OrderCohortsReport(OrderReportReader reader, ISiteService siteService, IClock clock, IStringLocalizer<OrderCohortsReport> stringLocalizer)
        : base(reader, siteService, clock, stringLocalizer)
    {
    }

    public static string Name => "order-cohorts";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<OrderCohortsReport>("Cohorts and retention");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<OrderCohortsReport>("Buyers by the month of their first order, and how many of them bought again in each of the six months after.");

    /// <inheritdoc/>
    public static string Category => ReportsConstants.Categories.Commerce;

    /// <inheritdoc/>
    public static Permission Permission => OrderPermissions.ManageOrders;

    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var (from, to) = GetPeriod(context);
        var timeZone = await GetTimeZoneAsync();

        // Each buyer's whole history, up to now, so a later month's return counts.
        var history = await ReadPaidAsync(_beginning, UtcNow, cancellationToken);
        var document = new ReportDocument { Title = DisplayName.Value };
        var cohorts = Build(history, from, to, timeZone);

        if (cohorts.Count == 0)
        {
            return document.Add(NoOrders());
        }

        var buyers = cohorts.Sum(cohort => cohort.Buyers);
        var returned = cohorts.Sum(cohort => cohort.ReturnedAny);

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["New buyers"].Value, ReportFormat.Number(buyers), S["Their first paid order was in the period."].Value),
            new ReportMetric(S["Came back"].Value, buyers == 0 ? "0%" : (returned / (decimal)buyers).ToString("P0", CultureInfo.CurrentCulture), S["{0} of them bought again within {1} months.", ReportFormat.Number(returned), MonthsAfter].Value),
        ]));

        var columns = new List<ReportColumn>
        {
            new(S["First order"].Value),
            new(S["Buyers"].Value, ReportColumnAlign.End),
        };

        for (var month = 1; month <= MonthsAfter; month++)
        {
            columns.Add(new ReportColumn(S["Month {0}", month].Value, ReportColumnAlign.End));
        }

        document.Add(ReportSection.ForTable(S["Share who bought again, by month after their first"].Value, columns,
        [
            .. cohorts.Select(cohort =>
            {
                var cells = new List<string>
                {
                    cohort.Month.ToString("Y", CultureInfo.CurrentCulture),
                    ReportFormat.Number(cohort.Buyers),
                };

                for (var month = 1; month <= MonthsAfter; month++)
                {
                    // A month that hasn't come yet is left blank, not 0%.
                    cells.Add(cohort.Returned.TryGetValue(month, out var count)
                        ? (count / (decimal)cohort.Buyers).ToString("P0", CultureInfo.CurrentCulture)
                        : string.Empty);
                }

                return new ReportRow(cells);
            }),
        ]));

        return document;
    }

    /// <summary>
    /// Groups the buyers whose first paid order fell in the period by its month, and counts, for each month after, those
    /// who paid for another order in it. A month that hasn't ended is left out.
    /// </summary>
    internal IReadOnlyList<Cohort> Build(IEnumerable<Order> history, DateTime fromUtc, DateTime toUtc, TimeZoneInfo timeZone)
    {
        static DateTime MonthOf(DateTime utc, TimeZoneInfo zone)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

            return new DateTime(local.Year, local.Month, 1);
        }

        var nowMonth = MonthOf(UtcNow, timeZone);
        var byBuyer = history
            .Where(order => order.PaidUtc is not null)
            .Select(order => (Key: OrderReportAggregator.BuyerKey(order), Paid: order.PaidUtc.Value))
            .Where(entry => entry.Key is not null)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal);

        var cohorts = new Dictionary<DateTime, Cohort>();

        foreach (var buyer in byBuyer)
        {
            var first = buyer.Min(entry => entry.Paid);

            if (first < fromUtc || first >= toUtc)
            {
                continue;
            }

            var cohortMonth = MonthOf(first, timeZone);

            if (!cohorts.TryGetValue(cohortMonth, out var cohort))
            {
                cohort = new Cohort(cohortMonth);
                cohorts[cohortMonth] = cohort;
            }

            cohort.Buyers++;

            var returnMonths = buyer
                .Select(entry => MonthOf(entry.Paid, timeZone))
                .Select(month => ((month.Year - cohortMonth.Year) * 12) + month.Month - cohortMonth.Month)
                .Where(offset => offset is >= 1 and <= MonthsAfter)
                .Distinct()
                .ToList();

            foreach (var offset in returnMonths)
            {
                cohort.Count(offset);
            }

            if (returnMonths.Count > 0)
            {
                cohort.ReturnedAny++;
            }
        }

        foreach (var cohort in cohorts.Values)
        {
            // Months up to the last one that has ended show, even with no one back.
            for (var month = 1; month <= MonthsAfter && cohort.Month.AddMonths(month) < nowMonth; month++)
            {
                cohort.Returned.TryAdd(month, 0);
            }

            foreach (var future in cohort.Returned.Keys.Where(month => cohort.Month.AddMonths(month) >= nowMonth).ToList())
            {
                cohort.Returned.Remove(future);
            }
        }

        return [.. cohorts.Values.OrderBy(cohort => cohort.Month)];
    }

    internal sealed class Cohort
    {
        public Cohort(DateTime month)
        {
            Month = month;
        }

        public DateTime Month { get; }

        public int Buyers { get; set; }

        /// <summary>
        /// Gets the buyers who bought again, by the month after their first.
        /// </summary>
        public Dictionary<int, int> Returned { get; } = [];

        /// <summary>
        /// Gets or sets the buyers who bought again in any of the months after, each counted once.
        /// </summary>
        public int ReturnedAny { get; set; }

        public void Count(int offset)
            => Returned[offset] = Returned.GetValueOrDefault(offset) + 1;
    }
}
