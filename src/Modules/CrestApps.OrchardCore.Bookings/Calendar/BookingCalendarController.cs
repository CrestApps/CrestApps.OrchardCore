using CrestApps.OrchardCore.Bookings.Controllers;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Mvc.Core.Utilities;

namespace CrestApps.OrchardCore.Bookings.Calendar;

/// <summary>
/// Puts every booking on the Bookings calendar, such as the bookings made before the Calendars feature was turned on.
/// </summary>
public sealed class BookingCalendarController : Controller
{
    private const int PageSize = 200;
    private const int MaxPages = 500;

    private readonly IAuthorizationService _authorizationService;
    private readonly IBookingStore _bookingStore;
    private readonly IClock _clock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingCalendarController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="bookingStore">The booking store.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    public BookingCalendarController(
        IAuthorizationService authorizationService,
        IBookingStore bookingStore,
        IClock clock,
        INotifier notifier,
        IHtmlLocalizer<BookingCalendarController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _bookingStore = bookingStore;
        _clock = clock;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Adds or updates the calendar event of every booking.
    /// </summary>
    /// <returns>A redirect to the bookings calendar.</returns>
    [HttpPost]
    // Not under "bookings/", where BookingsAdminController's "bookings/{action}/{itemId?}" would take it as its Calendar action.
    [Admin("bookings-calendar/sync", "BookingsCalendarSync")]
    public async Task<IActionResult> Sync()
    {
        if (!await _authorizationService.AuthorizeAsync(User, BookingsPermissions.ManageBookings))
        {
            return Forbid();
        }

        // The handler is only there while the Calendars feature is on.
        var handler = HttpContext.RequestServices.GetService<BookingCalendarHandler>();

        if (handler is null)
        {
            return NotFound();
        }

        if (await handler.GetCalendarAsync() is null)
        {
            await _notifier.ErrorAsync(H["The Bookings calendar couldn't be created."]);

            return RedirectToCalendar();
        }

        var changed = 0;
        var utcNow = _clock.UtcNow;

        // The default listing leaves out unpaid orders, so the held bookings are listed on their own.
        foreach (var query in new[] { new BookingQuery(), new BookingQuery { Status = BookingStatus.Held } })
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var (_, bookings) = await _bookingStore.PageAsync(query, utcNow, page * PageSize, PageSize, HttpContext.RequestAborted);

                foreach (var booking in bookings)
                {
                    if (await handler.SyncAsync(booking))
                    {
                        changed++;
                    }
                }

                if (bookings.Count < PageSize)
                {
                    break;
                }
            }
        }

        await _notifier.SuccessAsync(H["{0} bookings were added to or updated on the Bookings calendar.", changed]);

        return RedirectToCalendar();
    }

    private RedirectToActionResult RedirectToCalendar()
    {
        return RedirectToAction(nameof(BookingsAdminController.Calendar), typeof(BookingsAdminController).ControllerName(), new { area = BookingsConstants.Feature });
    }
}
