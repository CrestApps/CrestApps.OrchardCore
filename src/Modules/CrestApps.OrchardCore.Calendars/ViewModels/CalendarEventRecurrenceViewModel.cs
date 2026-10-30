using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The Repeat card of the event editor.
/// </summary>
public class CalendarEventRecurrenceViewModel
{
    /// <summary>
    /// Gets or sets how often the event repeats: empty for never, or DAILY, WEEKLY, MONTHLY or YEARLY.
    /// </summary>
    public string Frequency { get; set; }

    /// <summary>
    /// Gets or sets every how many periods it repeats.
    /// </summary>
    public int Interval { get; set; } = 1;

    /// <summary>
    /// Gets or sets the days of the week a weekly event repeats on, as MO, TU and so on.
    /// </summary>
    public string[] WeekDays { get; set; } = [];

    /// <summary>
    /// Gets or sets how a monthly event repeats: <c>day</c> for the same day of the month, <c>weekday</c> for the same weekday
    /// of the same week (such as the second Tuesday).
    /// </summary>
    public string MonthlyMode { get; set; } = "day";

    /// <summary>
    /// Gets or sets how the series ends: <c>never</c>, <c>on</c> a date, or <c>after</c> a number of occurrences.
    /// </summary>
    public string Ends { get; set; } = "never";

    /// <summary>
    /// Gets or sets the last date, as yyyy-MM-dd.
    /// </summary>
    public string Until { get; set; }

    /// <summary>
    /// Gets or sets the number of occurrences.
    /// </summary>
    public int Count { get; set; } = 10;

    /// <summary>
    /// Gets or sets a custom RFC 5545 rule, used instead of the choices above when it is filled in.
    /// </summary>
    public string CustomRule { get; set; }

    /// <summary>
    /// Gets or sets whether the event is one occurrence being changed, which can't change how the series repeats.
    /// </summary>
    [BindNever]
    public bool IsOccurrence { get; set; }
}
