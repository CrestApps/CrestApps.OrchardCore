using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The pay periods timesheets follow, and the week overtime counts in.
/// </summary>
public sealed class PayPeriodCalendar : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how often workers are paid.
    /// </summary>
    public PayPeriodFrequency Frequency { get; set; } = PayPeriodFrequency.Biweekly;

    /// <summary>
    /// Gets or sets the first day of any one period, for weekly and two-weekly periods.
    /// </summary>
    public DateOnly AnchorDate { get; set; } = new(2026, 1, 4);

    /// <summary>
    /// Gets or sets the day the overtime week starts.
    /// </summary>
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Gets or sets whether the calendar is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns the pay period a day falls in.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The first and last days of the period.</returns>
    public (DateOnly Start, DateOnly End) PeriodOn(DateOnly date)
    {
        switch (Frequency)
        {
            case PayPeriodFrequency.Monthly:
                {
                    var start = new DateOnly(date.Year, date.Month, 1);

                    return (start, start.AddMonths(1).AddDays(-1));
                }

            case PayPeriodFrequency.SemiMonthly:
                {
                    return date.Day <= 15
                        ? (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, 15))
                        : (new DateOnly(date.Year, date.Month, 16), new DateOnly(date.Year, date.Month, 1).AddMonths(1).AddDays(-1));
                }

            default:
                {
                    var length = Frequency == PayPeriodFrequency.Weekly ? 7 : 14;
                    var offset = date.DayNumber - AnchorDate.DayNumber;
                    var index = (int)Math.Floor(offset / (double)length);
                    var start = AnchorDate.AddDays(index * length);

                    return (start, start.AddDays(length - 1));
                }
        }
    }

    /// <summary>
    /// Returns the first day of the overtime week a day falls in.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The week's first day.</returns>
    public DateOnly WeekOf(DateOnly date)
    {
        return date.AddDays(-(((int)date.DayOfWeek - (int)WeekStart + 7) % 7));
    }
}
