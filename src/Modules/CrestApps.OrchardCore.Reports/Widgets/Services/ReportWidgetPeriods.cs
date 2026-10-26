namespace CrestApps.OrchardCore.Reports.Widgets.Services;

/// <summary>
/// Turns the period of a report dashboard widget into the dates its report covers.
/// </summary>
public static class ReportWidgetPeriods
{
    /// <summary>
    /// Resolves a period against the current local time.
    /// </summary>
    /// <param name="period">The period.</param>
    /// <param name="localNow">The current time in the site's time zone.</param>
    /// <param name="customFrom">The first day of a <see cref="ReportWidgetPeriod.Custom"/> period, in the site's time zone.</param>
    /// <param name="customTo">The last day of a <see cref="ReportWidgetPeriod.Custom"/> period, in the site's time zone.</param>
    /// <returns>
    /// The first and last moments the period covers, in the site's time zone; a bound that is
    /// <see langword="null"/> is open.
    /// </returns>
    public static (DateTime? From, DateTime? To) Resolve(ReportWidgetPeriod period, DateTime localNow, DateTime? customFrom = null, DateTime? customTo = null)
    {
        var today = localNow.Date;
        var endOfToday = today.AddDays(1).AddTicks(-1);
        var firstOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        switch (period)
        {
            case ReportWidgetPeriod.Today:
                return (today, endOfToday);
            case ReportWidgetPeriod.Yesterday:
                return (today.AddDays(-1), today.AddTicks(-1));
            case ReportWidgetPeriod.Last7Days:
                return (today.AddDays(-6), endOfToday);
            case ReportWidgetPeriod.Last30Days:
                return (today.AddDays(-29), endOfToday);
            case ReportWidgetPeriod.Last90Days:
                return (today.AddDays(-89), endOfToday);
            case ReportWidgetPeriod.ThisMonth:
                return (firstOfMonth, endOfToday);
            case ReportWidgetPeriod.LastMonth:
                return (firstOfMonth.AddMonths(-1), firstOfMonth.AddTicks(-1));
            case ReportWidgetPeriod.ThisYear:
                return (new DateTime(today.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), endOfToday);
            case ReportWidgetPeriod.Last12Months:
                return (firstOfMonth.AddMonths(-11), endOfToday);
            case ReportWidgetPeriod.AllTime:
                return (null, null);
            default:
                {
                    var from = customFrom?.Date;
                    var to = customTo?.Date.AddDays(1).AddTicks(-1);

                    if (from.HasValue && to.HasValue && from > to)
                    {
                        (from, to) = (to.Value.Date, from.Value.AddDays(1).AddTicks(-1));
                    }

                    return (from, to);
                }
        }
    }
}
