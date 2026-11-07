using System.Globalization;

namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// The date range a report starts on before a person picks one, declared with <see cref="IReport.DefaultDateRange"/>.
/// A report that declares one also accepts open-ended ranges: an empty bound stays empty, so "on or after" and "on or
/// before" reach the report as they were picked, and <see cref="AllTime"/> reaches it with neither bound. A report that
/// declares none starts on today, and the built-in date range filter fills an empty bound with today's.
/// </summary>
public sealed class ReportDefaultDateRange
{
    /// <summary>
    /// The key of the date range choice that sets neither bound.
    /// </summary>
    public const string AllTimeKey = "all";

    /// <summary>
    /// The key of the date range choice of a period of the report's own, such as a custom range.
    /// </summary>
    public const string CustomKey = "custom";

    private ReportDefaultDateRange(ReportDateRangePeriod period, int days, DayOfWeek firstDayOfWeek)
    {
        Period = period;
        Days = days;
        FirstDayOfWeek = firstDayOfWeek;
    }

    /// <summary>
    /// Gets today, from the start to the end of the day.
    /// </summary>
    public static ReportDefaultDateRange Today { get; } = new(ReportDateRangePeriod.Today, 1, DayOfWeek.Monday);

    /// <summary>
    /// Gets every date: neither bound is set.
    /// </summary>
    public static ReportDefaultDateRange AllTime { get; } = new(ReportDateRangePeriod.AllTime, 0, DayOfWeek.Monday);

    /// <summary>
    /// Gets the kind of period.
    /// </summary>
    public ReportDateRangePeriod Period { get; }

    /// <summary>
    /// Gets the number of days of a <see cref="ReportDateRangePeriod.LastDays"/> period.
    /// </summary>
    public int Days { get; }

    /// <summary>
    /// Gets the first day of a <see cref="ReportDateRangePeriod.ThisWeek"/> period.
    /// </summary>
    public DayOfWeek FirstDayOfWeek { get; }

    /// <summary>
    /// Gets the key of the date range choice the period matches, so the date range selector shows it by name: one of
    /// <c>today</c>, <see cref="AllTimeKey"/>, <c>last7</c>, <c>last30</c>, <c>last90</c> or <c>thisWeek</c>, or
    /// <see cref="CustomKey"/> for a number of days the selector has no choice for.
    /// </summary>
    public string Key => Period switch
    {
        ReportDateRangePeriod.AllTime => AllTimeKey,
        ReportDateRangePeriod.LastDays when Days is 7 or 30 or 90 => "last" + Days.ToString(CultureInfo.InvariantCulture),
        ReportDateRangePeriod.LastDays => CustomKey,
        ReportDateRangePeriod.ThisWeek => "thisWeek",
        _ => "today",
    };

    /// <summary>
    /// Gets the last days up to the end of today, today included: <c>LastDays(7)</c> starts six days before today.
    /// </summary>
    /// <param name="days">The number of days, at least one.</param>
    /// <returns>The period.</returns>
    public static ReportDefaultDateRange LastDays(int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);

        return new ReportDefaultDateRange(ReportDateRangePeriod.LastDays, days, DayOfWeek.Monday);
    }

    /// <summary>
    /// Gets the current week, from its first day to the end of its seventh day.
    /// </summary>
    /// <param name="firstDayOfWeek">The day a week starts on.</param>
    /// <returns>The period.</returns>
    public static ReportDefaultDateRange ThisWeek(DayOfWeek firstDayOfWeek = DayOfWeek.Monday)
        => new(ReportDateRangePeriod.ThisWeek, 7, firstDayOfWeek);

    /// <summary>
    /// Works out the bounds of the period at a moment, in the same time zone as that moment.
    /// </summary>
    /// <param name="localNow">The current date and time, in the time zone the period is in.</param>
    /// <returns>The inclusive bounds, in the time zone of <paramref name="localNow"/>; both are
    /// <see langword="null"/> for <see cref="AllTime"/>.</returns>
    public (DateTime? From, DateTime? To) Resolve(DateTime localNow)
    {
        var today = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var endOfToday = today.AddDays(1).AddTicks(-1);

        switch (Period)
        {
            case ReportDateRangePeriod.AllTime:
                return (null, null);

            case ReportDateRangePeriod.LastDays:
                return (today.AddDays(-(Days - 1)), endOfToday);

            case ReportDateRangePeriod.ThisWeek:
                var first = today.AddDays(-(((int)today.DayOfWeek - (int)FirstDayOfWeek + 7) % 7));

                return (first, first.AddDays(7).AddTicks(-1));

            default:
                return (today, endOfToday);
        }
    }
}
