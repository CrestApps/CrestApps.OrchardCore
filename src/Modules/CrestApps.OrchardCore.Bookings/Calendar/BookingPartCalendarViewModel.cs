using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Bookings.Calendar;

/// <summary>
/// The calendar a bookable product is blocked with.
/// </summary>
public class BookingPartCalendarViewModel
{
    /// <summary>
    /// Gets or sets the calendar whose busy times close the product's dates and times.
    /// </summary>
    public string BlockingCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the calendars to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Calendars { get; set; } = [];
}
