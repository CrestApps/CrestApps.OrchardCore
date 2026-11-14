using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// How often people are paid: the periods, the week overtime is counted by, and the pay date of each period.
/// Workforce's time clock builds timesheets by its periods; Payroll runs pay by them.
/// </summary>
public sealed class PaySchedule : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name, such as Every two weeks.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how often a period starts.
    /// </summary>
    public PayFrequency Frequency { get; set; } = PayFrequency.Biweekly;

    /// <summary>
    /// Gets or sets a day a period starts on, for weekly, every-two-weeks and every-four-weeks schedules; its month
    /// starts quarterly and yearly ones.
    /// </summary>
    public DateOnly AnchorDate { get; set; } = new(2026, 1, 4);

    /// <summary>
    /// Gets or sets the day the overtime week starts.
    /// </summary>
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Sunday;

    /// <summary>
    /// Gets or sets the day of the month the second period of a twice-a-month schedule starts on.
    /// </summary>
    public int SecondPeriodStartDay { get; set; } = 16;

    /// <summary>
    /// Gets or sets how the pay date is found.
    /// </summary>
    public PayDateRule PayDateRule { get; set; }

    /// <summary>
    /// Gets or sets the days after the period ends that pay is made, for <see cref="PayDateRule.DaysAfterPeriodEnd"/>.
    /// </summary>
    public int PayDateOffsetDays { get; set; } = 5;

    /// <summary>
    /// Gets or sets the day of the month the first (or only) period of a month is paid, for
    /// <see cref="PayDateRule.FixedDays"/>; 0 is the last day of the month.
    /// </summary>
    public int FirstPayDay { get; set; } = 15;

    /// <summary>
    /// Gets or sets the day of the month the second period of a twice-a-month schedule is paid; 0 is the last day.
    /// </summary>
    public int SecondPayDay { get; set; }

    /// <summary>
    /// Gets or sets where a pay date on a day off moves.
    /// </summary>
    public PayDateShift NonBusinessDayShift { get; set; }

    /// <summary>
    /// Gets or sets the business hours schedule whose closed days are days off, or <see langword="null"/> for weekends.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets how many days before the pay date a run must be approved.
    /// </summary>
    public int ApprovalLeadDays { get; set; } = 2;

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was added.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the period a day falls in.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The first and last day of the period.</returns>
    public (DateOnly Start, DateOnly End) PeriodOn(DateOnly date)
    {
        switch (Frequency)
        {
            case PayFrequency.Monthly:
                {
                    var start = new DateOnly(date.Year, date.Month, 1);

                    return (start, start.AddMonths(1).AddDays(-1));
                }

            case PayFrequency.SemiMonthly:
                {
                    var monthStart = new DateOnly(date.Year, date.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                    var second = Math.Clamp(SecondPeriodStartDay, 2, 28);

                    return date.Day < second
                        ? (monthStart, new DateOnly(date.Year, date.Month, second - 1))
                        : (new DateOnly(date.Year, date.Month, second), monthEnd);
                }

            case PayFrequency.Quarterly:
            case PayFrequency.Yearly:
                {
                    var months = Frequency == PayFrequency.Quarterly ? 3 : 12;
                    var anchor = new DateOnly(AnchorDate.Year, AnchorDate.Month, 1);
                    var offset = ((date.Year - anchor.Year) * 12) + date.Month - anchor.Month;
                    var index = (int)Math.Floor(offset / (double)months);
                    var start = anchor.AddMonths(index * months);

                    return (start, start.AddMonths(months).AddDays(-1));
                }

            default:
                {
                    var length = Frequency switch
                    {
                        PayFrequency.Weekly => 7,
                        PayFrequency.FourWeekly => 28,
                        _ => 14,
                    };
                    var offset = date.DayNumber - AnchorDate.DayNumber;
                    var index = (int)Math.Floor(offset / (double)length);
                    var start = AnchorDate.AddDays(index * length);

                    return (start, start.AddDays(length - 1));
                }
        }
    }

    /// <summary>
    /// Gets the first day of the overtime week a day falls in.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The week's first day.</returns>
    public DateOnly WeekOf(DateOnly date)
        => date.AddDays(-(((int)date.DayOfWeek - (int)WeekStart + 7) % 7));

    /// <summary>
    /// Gets the pay date of the period that ends on a day.
    /// </summary>
    /// <param name="periodEnd">The period's last day.</param>
    /// <param name="isBusinessDay">Whether a day is a business day; weekends are days off when not given.</param>
    /// <returns>The pay date.</returns>
    public DateOnly PayDateFor(DateOnly periodEnd, Func<DateOnly, bool> isBusinessDay = null)
    {
        isBusinessDay ??= day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

        var date = PayDateRule == PayDateRule.FixedDays && Frequency is PayFrequency.Monthly or PayFrequency.SemiMonthly
            ? FixedPayDate(periodEnd)
            : periodEnd.AddDays(Math.Max(0, PayDateOffsetDays));

        if (NonBusinessDayShift == PayDateShift.None)
        {
            return date;
        }

        var step = NonBusinessDayShift == PayDateShift.Before ? -1 : 1;

        for (var tries = 0; tries < 14 && !isBusinessDay(date); tries++)
        {
            date = date.AddDays(step);
        }

        return date;
    }

    // The fixed day of the month the period is paid: the first period of a month on the first pay day, the second on
    // the second; 0 is the month's last day.
    private DateOnly FixedPayDate(DateOnly periodEnd)
    {
        var secondPeriod = Frequency == PayFrequency.SemiMonthly && periodEnd.Day >= Math.Clamp(SecondPeriodStartDay, 2, 28);
        var day = secondPeriod ? SecondPayDay : FirstPayDay;
        var monthEnd = new DateOnly(periodEnd.Year, periodEnd.Month, 1).AddMonths(1).AddDays(-1);

        return day <= 0 || day >= monthEnd.Day ? monthEnd : new DateOnly(periodEnd.Year, periodEnd.Month, day);
    }
}
