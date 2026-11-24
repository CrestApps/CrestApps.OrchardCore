using CrestApps.OrchardCore.Agreements.Models;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// How an agreement's hours went down in a period, day by day, and when they are expected to run out at the pace so
/// far: what the agreement reports draw and list.
/// </summary>
public static class AgreementBurnDown
{
    /// <summary>
    /// Lists each day of the balance's period up to <paramref name="through"/> (or the period's end): the hours used up to
    /// the end of that day, the hours left, and the hours an even pace would leave, which only a period with an end has.
    /// </summary>
    /// <param name="agreement">The agreement, whose time zone decides each entry's day.</param>
    /// <param name="balance">The agreement's balance for the period.</param>
    /// <param name="through">The last day to list, such as today.</param>
    /// <returns>The days, in order; empty when <paramref name="through"/> is before the period.</returns>
    public static IReadOnlyList<AgreementBurnDownDay> Days(Agreement agreement, AgreementBalance balance, DateOnly through)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(balance);

        var last = balance.PeriodEnd is { } end && end < through ? end : through;

        if (last < balance.PeriodStart)
        {
            return [];
        }

        var perDay = UsedPerDay(agreement, balance);
        var totalDays = balance.PeriodEnd is { } periodEnd ? periodEnd.DayNumber - balance.PeriodStart.DayNumber + 1 : (int?)null;
        var days = new List<AgreementBurnDownDay>();
        var used = 0d;

        for (var day = balance.PeriodStart; day <= last; day = day.AddDays(1))
        {
            used += perDay.GetValueOrDefault(day);
            var index = day.DayNumber - balance.PeriodStart.DayNumber + 1;

            days.Add(new AgreementBurnDownDay(
                day,
                used,
                Math.Max(0, balance.CoveredMinutes - used),
                totalDays is { } total ? balance.CoveredMinutes * (1 - ((double)index / total)) : null));
        }

        return days;
    }

    /// <summary>
    /// Finds the day the hours ran out, or are expected to at the pace from the period's start to
    /// <paramref name="through"/>.
    /// </summary>
    /// <param name="agreement">The agreement, whose time zone decides each entry's day.</param>
    /// <param name="balance">The agreement's balance for the period.</param>
    /// <param name="through">The day the pace is measured to, such as today.</param>
    /// <returns>The day, and whether the hours already ran out; no day when nothing is used yet or the hours last the period.</returns>
    public static AgreementRunOut ProjectRunOut(Agreement agreement, AgreementBalance balance, DateOnly through)
    {
        ArgumentNullException.ThrowIfNull(agreement);
        ArgumentNullException.ThrowIfNull(balance);

        if (balance.CoveredMinutes <= 0 || balance.UsedMinutes <= 0)
        {
            return new AgreementRunOut(null, false);
        }

        if (balance.UsedMinutes >= balance.CoveredMinutes)
        {
            var used = 0d;

            foreach (var (day, minutes) in UsedPerDay(agreement, balance).OrderBy(pair => pair.Key))
            {
                used += minutes;

                if (used >= balance.CoveredMinutes)
                {
                    return new AgreementRunOut(day, true);
                }
            }

            return new AgreementRunOut(null, true);
        }

        var last = balance.PeriodEnd is { } end && end < through ? end : through;
        var elapsed = Math.Max(1, last.DayNumber - balance.PeriodStart.DayNumber + 1);
        var perDay = balance.UsedMinutes / elapsed;
        var runOut = balance.PeriodStart.AddDays((int)Math.Ceiling(balance.CoveredMinutes / perDay) - 1);

        return balance.PeriodEnd is { } periodEnd && runOut > periodEnd
            ? new AgreementRunOut(null, false)
            : new AgreementRunOut(runOut, false);
    }

    private static Dictionary<DateOnly, double> UsedPerDay(Agreement agreement, AgreementBalance balance)
    {
        var perDay = new Dictionary<DateOnly, double>();

        foreach (var (usage, minutes) in balance.Entries)
        {
            var day = AgreementPeriods.DayOf(agreement, usage.StartUtc);
            perDay[day] = perDay.GetValueOrDefault(day) + minutes;
        }

        return perDay;
    }
}

/// <summary>
/// A day of an agreement's burn-down, in minutes.
/// </summary>
/// <param name="Day">The day, in the agreement's time zone.</param>
/// <param name="UsedMinutes">The minutes used from the period's start to the end of the day.</param>
/// <param name="LeftMinutes">The minutes left at the end of the day.</param>
/// <param name="EvenLeftMinutes">The minutes an even pace would leave at the end of the day; <see langword="null"/> for a period without an end.</param>
public sealed record AgreementBurnDownDay(DateOnly Day, double UsedMinutes, double LeftMinutes, double? EvenLeftMinutes);

/// <summary>
/// When an agreement's hours ran out or are expected to.
/// </summary>
/// <param name="Day">The day; <see langword="null"/> when nothing is used yet or the hours last the period.</param>
/// <param name="Reached">Whether the hours already ran out.</param>
public sealed record AgreementRunOut(DateOnly? Day, bool Reached);
