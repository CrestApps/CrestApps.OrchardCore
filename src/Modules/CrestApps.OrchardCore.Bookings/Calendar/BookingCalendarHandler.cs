using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Bookings.Calendar;

/// <summary>
/// Keeps a calendar event for every booking on the Bookings calendar: tentative while held or requested, confirmed once
/// booked, moved when the booking moves, and cancelled when the booking is canceled or its hold runs out. The events belong to
/// Bookings, so they can't be changed in the calendar.
/// </summary>
public sealed class BookingCalendarHandler : IBookingHandler
{
    /// <summary>
    /// The source of the calendar and the calendar events this feature creates.
    /// </summary>
    public const string Source = "Bookings";

    /// <summary>
    /// The calendar type of the Bookings calendar.
    /// </summary>
    public const string CalendarType = "Bookings";

    /// <summary>
    /// The event type of a booking's calendar event.
    /// </summary>
    public const string EventType = "Booking";

    private static readonly CalendarChangeOptions _changeOptions = new()
    {
        Actor = Source,
        SkipConflictCheck = true,
        SendInvitations = false,
    };

    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;

    private Calendars.Models.Calendar _calendar;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCalendarHandler"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventManager">The calendar event manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BookingCalendarHandler(
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        IStringLocalizer<BookingCalendarHandler> stringLocalizer)
    {
        _calendarService = calendarService;
        _eventManager = eventManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task SavingAsync(BookingSavingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return SyncAsync(context.Booking);
    }

    /// <inheritdoc />
    public async Task DeletingAsync(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (string.IsNullOrEmpty(booking.ItemId))
        {
            return;
        }

        foreach (var calendarEvent in await _eventManager.FindBySourceAsync(Source, booking.ItemId))
        {
            await _eventManager.DeleteAsync(calendarEvent, _changeOptions);
        }
    }

    /// <summary>
    /// Creates, moves or cancels a booking's calendar event so it matches the booking.
    /// </summary>
    /// <param name="booking">The booking.</param>
    /// <returns>Whether an event was created or changed.</returns>
    public async Task<bool> SyncAsync(Booking booking)
    {
        ArgumentNullException.ThrowIfNull(booking);

        if (string.IsNullOrEmpty(booking.ItemId) || booking.StartsUtc == default)
        {
            return false;
        }

        var events = await _eventManager.FindBySourceAsync(Source, booking.ItemId);
        var existing = events.Count > 0 ? events[0] : null;
        var cancelled = booking.Status is BookingStatus.Canceled or BookingStatus.Expired;

        if (existing is null && cancelled)
        {
            return false;
        }

        var status = GetStatus(booking.Status);
        var endUtc = booking.EndsUtc is { } ends && ends > booking.StartsUtc ? ends : booking.StartsUtc.AddHours(1);
        var title = string.IsNullOrWhiteSpace(booking.CustomerName) ? booking.Title : $"{booking.Title}: {booking.CustomerName}";

        if (existing is null)
        {
            var calendar = await GetCalendarAsync();

            if (calendar is null)
            {
                return false;
            }

            var calendarEvent = await _eventManager.NewAsync(calendar, EventType);

            Apply(calendarEvent, booking, title, status, endUtc);
            calendarEvent.Source = Source;
            calendarEvent.SourceId = booking.ItemId;
            calendarEvent.Editable = false;

            return (await _eventManager.CreateAsync(calendarEvent, _changeOptions)).Succeeded;
        }

        if (cancelled)
        {
            return existing.Status != CalendarEventStatus.Cancelled && (await _eventManager.CancelAsync(existing, _changeOptions)).Succeeded;
        }

        if (existing.Status == status && existing.StartUtc == booking.StartsUtc && existing.EndUtc == endUtc && existing.Title == title)
        {
            return false;
        }

        return (await _eventManager.UpdateAsync(existing, target => Apply(target, booking, title, status, endUtc), _changeOptions)).Succeeded;
    }

    /// <summary>
    /// Finds the Bookings calendar, without creating it.
    /// </summary>
    /// <returns>The calendar, or <see langword="null"/> when no booking was put on it yet.</returns>
    public async Task<Calendars.Models.Calendar> FindCalendarAsync()
    {
        return _calendar ??= await _calendarService.FindBySourceAsync(Source, Source);
    }

    /// <summary>
    /// Gets the Bookings calendar, creating it the first time.
    /// </summary>
    /// <returns>The calendar, or <see langword="null"/> when it can't be created.</returns>
    public async Task<Calendars.Models.Calendar> GetCalendarAsync()
    {
        if (await FindCalendarAsync() is { } existing)
        {
            return existing;
        }

        var calendar = await _calendarService.NewAsync(CalendarType);

        if (calendar is null)
        {
            return null;
        }

        calendar.Name = S["Bookings"];
        calendar.Source = Source;
        calendar.SourceId = Source;

        var result = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = Source });

        _calendar = result.Succeeded ? result.Value : null;

        return _calendar;
    }

    private static CalendarEventStatus GetStatus(BookingStatus status)
    {
        return status switch
        {
            BookingStatus.Held or BookingStatus.Requested => CalendarEventStatus.Tentative,
            BookingStatus.Canceled or BookingStatus.Expired => CalendarEventStatus.Cancelled,
            _ => CalendarEventStatus.Confirmed,
        };
    }

    private static void Apply(CalendarEvent target, Booking booking, string title, CalendarEventStatus status, DateTime endUtc)
    {
        target.Title = title;
        target.AllDay = false;
        target.Start = CalendarTime.ToLocal(booking.StartsUtc, target.TimeZoneId);
        target.End = CalendarTime.ToLocal(endUtc, target.TimeZoneId);
        target.Status = status;
        target.Transparency = CalendarTransparency.Busy;
        target.Description = string.IsNullOrEmpty(booking.OrderNumber) ? null : booking.OrderNumber;

        if (!string.IsNullOrWhiteSpace(booking.CustomerEmail) &&
            !target.Attendees.Any(attendee => string.Equals(attendee.Email, booking.CustomerEmail, StringComparison.OrdinalIgnoreCase)))
        {
            target.Attendees.Add(new CalendarAttendee
            {
                Email = booking.CustomerEmail,
                Name = booking.CustomerName,
                Role = CalendarAttendeeRole.Required,
                Response = CalendarAttendeeResponse.Accepted,
            });
        }
    }
}
