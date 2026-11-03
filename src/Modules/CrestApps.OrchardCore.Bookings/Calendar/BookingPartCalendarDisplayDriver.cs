using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Bookings.Calendar;

/// <summary>
/// Adds the calendar a bookable product is blocked with to its booking settings.
/// </summary>
internal sealed class BookingPartCalendarDisplayDriver : ContentPartDisplayDriver<BookingPart>
{
    private readonly ICalendarService _calendarService;

    internal readonly IStringLocalizer S;

    public BookingPartCalendarDisplayDriver(
        ICalendarService calendarService,
        IStringLocalizer<BookingPartCalendarDisplayDriver> stringLocalizer)
    {
        _calendarService = calendarService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(BookingPart part, BuildPartEditorContext context)
        => Initialize<BookingPartCalendarViewModel>("BookingPartCalendar_Edit", async model =>
        {
            model.BlockingCalendarId = part.BlockingCalendarId;
            model.Calendars = await ListCalendarsAsync(part.BlockingCalendarId);
        }).Location("Content:10");

    public override async Task<IDisplayResult> UpdateAsync(BookingPart part, UpdatePartEditorContext context)
    {
        var model = new BookingPartCalendarViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var calendarId = string.IsNullOrWhiteSpace(model.BlockingCalendarId) ? null : model.BlockingCalendarId.Trim();

        if (calendarId is not null && await _calendarService.FindByIdAsync(calendarId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.BlockingCalendarId), S["The calendar wasn't found."]);
        }
        else
        {
            part.BlockingCalendarId = calendarId;
        }

        return Edit(part, context);
    }

    private async Task<IList<SelectListItem>> ListCalendarsAsync(string selectedId)
    {
        // The Bookings calendar itself is left out: every booking is busy on it, so it would close the product to itself.
        return (await _calendarService.ListAsync(new CalendarListQuery { Limit = 500 }))
            .Where(calendar => calendar.Source != BookingCalendarHandler.Source)
            .OrderBy(calendar => calendar.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(calendar => new SelectListItem(calendar.Name, calendar.ItemId, calendar.ItemId == selectedId))
            .ToList();
    }
}
