using System.Globalization;
using CrestApps.OrchardCore.Bookings.Models;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// The rules of a booking product's dates: which dates and times are offered, which dates a booking takes, what one unit
/// costs, and what canceling costs. Dates are in the site's time zone.
/// </summary>
public static class BookingSchedule
{
    public const string DateFormat = "yyyy-MM-dd";

    public const string TimeFormat = "yyyy-MM-dd'T'HH:mm";

    public static bool TryParseDate(string value, out DateOnly date)
        => DateOnly.TryParseExact(value?.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool TryParseTime(string value, out DateTime local)
        => DateTime.TryParseExact(value?.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    public static string ToKey(DateOnly date)
        => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string ToKey(DateTime local)
        => local.ToString(TimeFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Returns the dates a booking takes: the departure, every night from check-in to the night before check-out, or the
    /// time slot. Nothing for dates that don't read right.
    /// </summary>
    public static IReadOnlyList<string> GetSlotKeys(BookingKind kind, string date, string end)
    {
        switch (kind)
        {
            case BookingKind.TimeSlot:
                return TryParseTime(date, out var time) ? [ToKey(time)] : [];

            case BookingKind.Nightly:
                if (!TryParseDate(date, out var checkIn) || !TryParseDate(end, out var checkOut) || checkOut <= checkIn)
                {
                    return [];
                }

                var nights = new List<string>();

                for (var night = checkIn; night < checkOut; night = night.AddDays(1))
                {
                    nights.Add(ToKey(night));
                }

                return nights;

            default:
                return TryParseDate(date, out var departure) ? [ToKey(departure)] : [];
        }
    }

    /// <summary>
    /// Returns the local start of a date or time key.
    /// </summary>
    public static DateTime? GetLocalStart(string slotKey)
        => TryParseTime(slotKey, out var time)
            ? time
            : TryParseDate(slotKey, out var date)
                ? date.ToDateTime(TimeOnly.MinValue)
                : null;

    /// <summary>
    /// Returns when a date or time key starts, in UTC.
    /// </summary>
    public static DateTime? GetStartUtc(string slotKey, TimeZoneInfo timeZone)
        => GetLocalStart(slotKey) is { } local
            ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone ?? TimeZoneInfo.Utc)
            : null;

    /// <summary>
    /// Returns the times a product's slots start on a day: from its opening to its closing, every slot's length.
    /// </summary>
    public static IReadOnlyList<DateTime> GetTimes(BookingPart part, DateOnly day)
    {
        var minutes = Math.Max(5, part?.SlotMinutes ?? 60);
        var times = new List<DateTime>();

        foreach (var hours in part?.Hours ?? [])
        {
            if (hours.Day != day.DayOfWeek ||
                !TimeOnly.TryParseExact(hours.Opens, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opens) ||
                !TimeOnly.TryParseExact(hours.Closes, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var closes))
            {
                continue;
            }

            for (var start = day.ToDateTime(opens); start.AddMinutes(minutes) <= day.ToDateTime(closes); start = start.AddMinutes(minutes))
            {
                times.Add(start);
            }
        }

        return [.. times.Distinct().Order()];
    }

    /// <summary>
    /// Returns why a booking's dates can't be booked by the product's rules, as a code (<c>past</c>, <c>far</c>,
    /// <c>nights</c>, <c>closed</c>), or <see langword="null"/> when they can. A departure's dates are the ones the store
    /// added, which <see cref="BookingCapacityService"/> checks.
    /// </summary>
    public static string Check(BookingPart part, string date, string end, DateTime localNow)
    {
        var keys = GetSlotKeys(part.Kind, date, end);

        if (keys.Count == 0)
        {
            return "closed";
        }

        var today = DateOnly.FromDateTime(localNow);
        var first = GetLocalStart(keys[0]).Value;

        if (part.Kind == BookingKind.TimeSlot ? first <= localNow : DateOnly.FromDateTime(first) < today)
        {
            return "past";
        }

        if (part.Kind != BookingKind.Departure && DateOnly.FromDateTime(first) > today.AddDays(Math.Max(1, part.DaysAhead)))
        {
            return "far";
        }

        if (part.Kind == BookingKind.Nightly && (keys.Count < Math.Max(1, part.MinNights) || keys.Count > Math.Max(part.MinNights, part.MaxNights)))
        {
            return "nights";
        }

        if (part.Kind == BookingKind.TimeSlot && !GetTimes(part, DateOnly.FromDateTime(first)).Contains(first))
        {
            return "closed";
        }

        return null;
    }

    /// <summary>
    /// Returns whether a night is a Friday or a Saturday, priced at the weekend price.
    /// </summary>
    public static bool IsWeekendNight(string slotKey)
        => TryParseDate(slotKey, out var night) && night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    /// <summary>
    /// Returns what one unit costs: the price of each of its dates, for each person when the price is per person, and
    /// the occupancy's adjustment once.
    /// </summary>
    public static decimal GetUnitPrice(BookingPart part, int people, IEnumerable<decimal> datePrices, decimal adjustment)
    {
        var prices = datePrices?.Sum() ?? 0m;
        var unit = (part?.PricePerPerson == true ? prices * Math.Max(1, people) : prices) + adjustment;

        return Math.Max(0m, Math.Round(unit, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Returns how much of each date one unit takes: a seat for each person on a departure, one otherwise.
    /// </summary>
    public static int GetSeatsPerUnit(BookingKind kind, int people)
        => kind == BookingKind.Departure ? Math.Max(1, people) : 1;

    /// <summary>
    /// Returns the percent of the price the store keeps when a booking is canceled <paramref name="daysBefore"/> days
    /// before its date: the tier for the most days it's still within, or everything closer than every tier. Without
    /// tiers, nothing.
    /// </summary>
    public static decimal GetCancellationFeePercent(IEnumerable<BookingCancellationTier> tiers, int daysBefore)
    {
        var ordered = tiers?.Where(tier => tier is not null && tier.DaysBefore >= 0).OrderByDescending(tier => tier.DaysBefore).ToList();

        if (ordered is not { Count: > 0 })
        {
            return 0m;
        }

        foreach (var tier in ordered)
        {
            if (daysBefore >= tier.DaysBefore)
            {
                return Math.Clamp(tier.FeePercent, 0m, 100m);
            }
        }

        return 100m;
    }

    /// <summary>
    /// Returns what's refunded when a booking is canceled: what was paid toward it, less the fee on its price.
    /// </summary>
    public static decimal GetRefund(decimal price, decimal paid, decimal feePercent)
    {
        var fee = Math.Round(price * Math.Clamp(feePercent, 0m, 100m) / 100m, 2, MidpointRounding.AwayFromZero);

        return Math.Max(0m, Math.Round(paid - fee, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Returns when the balance of a booking paid by deposit is due: some days before its date, at the start of the day.
    /// </summary>
    public static DateOnly GetBalanceDueDate(DateOnly start, int daysBefore)
        => start.AddDays(-Math.Max(0, daysBefore));
}
