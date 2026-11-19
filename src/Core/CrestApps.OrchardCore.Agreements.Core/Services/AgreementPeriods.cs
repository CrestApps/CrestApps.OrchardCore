using CrestApps.OrchardCore.Agreements.Models;
using NodaTime;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// The days, periods and rounding of agreements. Pure.
/// </summary>
public static class AgreementPeriods
{
    /// <summary>
    /// Gives a time zone by id, or UTC when it isn't known.
    /// </summary>
    /// <param name="timeZoneId">The time zone.</param>
    /// <returns>The time zone.</returns>
    public static DateTimeZone ZoneOf(string timeZoneId)
        => string.IsNullOrEmpty(timeZoneId) ? DateTimeZone.Utc : DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId) ?? DateTimeZone.Utc;

    /// <summary>
    /// Gives the day it is at a moment in an agreement's time zone.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="utc">The moment, in UTC.</param>
    /// <returns>The day.</returns>
    public static DateOnly DayOf(Agreement agreement, DateTime utc)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        var date = Instant.FromDateTimeUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).InZone(ZoneOf(agreement.TimeZoneId)).Date;

        return new DateOnly(date.Year, date.Month, date.Day);
    }

    /// <summary>
    /// Gives the period of an agreement's hours that holds a day: its whole period for a block, the calendar month
    /// (within its period) for an allowance.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="date">The day.</param>
    /// <returns>The first and last day, the last <see langword="null"/> for a block with no end.</returns>
    public static (DateOnly Start, DateOnly? End) PeriodOf(Agreement agreement, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        if (agreement.Hours?.Kind != AgreementHoursKind.MonthlyAllowance)
        {
            return (agreement.StartDate, agreement.EndDate);
        }

        var first = new DateOnly(date.Year, date.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        return (first < agreement.StartDate ? agreement.StartDate : first, agreement.EndDate < last ? agreement.EndDate : last);
    }

    /// <summary>
    /// Gives the moment a day starts in an agreement's time zone, in UTC.
    /// </summary>
    /// <param name="agreement">The agreement.</param>
    /// <param name="date">The day.</param>
    /// <returns>The moment.</returns>
    public static DateTime StartOfDayUtc(Agreement agreement, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(agreement);

        return ZoneOf(agreement.TimeZoneId).AtStartOfDay(LocalDate.FromDateOnly(date)).ToDateTimeUtc();
    }

    /// <summary>
    /// Rounds an entry of time up to the agreement's rounding.
    /// </summary>
    /// <param name="minutes">The minutes.</param>
    /// <param name="roundingMinutes">What to round up to; 0 keeps the minutes as they are.</param>
    /// <returns>The minutes counted.</returns>
    public static double Round(double minutes, int roundingMinutes)
        => minutes <= 0
            ? 0
            : roundingMinutes <= 0 ? minutes : Math.Ceiling(Math.Round(minutes, 6) / roundingMinutes) * roundingMinutes;
}
