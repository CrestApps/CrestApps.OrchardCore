using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Bookings.Calendar;

/// <summary>
/// Closes a product's dates and times that are busy on the calendar its booking settings block it with, such as a room's
/// calendar: a time slot is closed when anything busy overlaps it, and a date or night when anything busy falls on that day.
/// </summary>
public sealed class BookingCalendarSlotFilter : IBookingSlotFilter
{
    private readonly ICalendarAvailabilityService _availabilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCalendarSlotFilter"/> class.
    /// </summary>
    /// <param name="availabilityService">The calendar availability service.</param>
    public BookingCalendarSlotFilter(ICalendarAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>> GetClosedSlotKeysAsync(string contentItemId, BookingPart part, IReadOnlyList<string> slotKeys, TimeZoneInfo timeZone, CancellationToken cancellationToken = default)
    {
        if (part is null || string.IsNullOrEmpty(part.BlockingCalendarId) || slotKeys is not { Count: > 0 })
        {
            return [];
        }

        var windows = new List<(string Key, DateTime StartUtc, DateTime EndUtc)>();

        foreach (var key in slotKeys)
        {
            if (BookingSchedule.GetLocalStart(key) is not { } localStart)
            {
                continue;
            }

            // A time key holds a time slot; a date key holds a whole day.
            var hasTime = BookingSchedule.TryParseTime(key, out _);
            var localEnd = hasTime ? localStart.AddMinutes(Math.Max(1, part.SlotMinutes)) : localStart.AddDays(1);

            windows.Add((key, ToUtc(localStart, timeZone), ToUtc(localEnd, timeZone)));
        }

        if (windows.Count == 0)
        {
            return [];
        }

        var busy = await _availabilityService.GetBusyAsync(new CalendarBusyQuery
        {
            CalendarIds = [part.BlockingCalendarId],
            FromUtc = windows.Min(window => window.StartUtc),
            ToUtc = windows.Max(window => window.EndUtc),
            IncludeTentative = true,
            IncludeBusyTimeSources = false,
        }, cancellationToken);

        if (busy.Count == 0)
        {
            return [];
        }

        return windows
            .Where(window => busy.Any(interval => interval.StartUtc < window.EndUtc && interval.EndUtc > window.StartUtc))
            .Select(window => window.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone ?? TimeZoneInfo.Utc);
    }
}
