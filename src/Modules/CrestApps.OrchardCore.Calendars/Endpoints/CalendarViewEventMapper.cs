using System.Globalization;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Calendars.Endpoints;

/// <summary>
/// Turns an occurrence into what the calendar view draws: an occurrence the viewer may only see as busy keeps its time and
/// loses its title and details, and only an occurrence on a calendar the viewer can write to can be moved or edited.
/// </summary>
internal sealed class CalendarViewEventMapper
{
    private readonly HttpContext _httpContext;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly LinkGenerator _linkGenerator;
    private readonly IStringLocalizer S;

    private Dictionary<string, CalendarCategory> _categories = new(StringComparer.Ordinal);

    public CalendarViewEventMapper(
        HttpContext httpContext,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        LinkGenerator linkGenerator,
        IStringLocalizer stringLocalizer)
    {
        _httpContext = httpContext;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    // The categories of the occurrences about to be mapped, looked up once.
    public async Task LoadCategoriesAsync(IEnumerable<CalendarOccurrence> occurrences, ICalendarCategoryService categoryService, CancellationToken cancellationToken)
    {
        var ids = occurrences.Select(occurrence => occurrence.Event.CategoryId).Where(id => !string.IsNullOrEmpty(id));

        _categories = (await categoryService.GetAsync(ids, cancellationToken)).ToDictionary(category => category.ItemId, StringComparer.Ordinal);
    }

    public CalendarViewEvent Map(CalendarOccurrence occurrence, CalendarAccess access)
    {
        var calendarEvent = occurrence.Event;
        var calendar = access?.Calendar;
        var eventType = calendar is null ? null : _typeManager.FindEventType(calendar.Type, calendarEvent.EventType);
        var category = occurrence.BusyOnly || string.IsNullOrEmpty(calendarEvent.CategoryId) ? null : _categories.GetValueOrDefault(calendarEvent.CategoryId);
        var canChange = calendar is not null && !occurrence.BusyOnly && calendarEvent.Editable && !string.IsNullOrEmpty(calendarEvent.ItemId) && access.Level >= CalendarAccessLevel.Write;
        var editUrl = canChange
            ? _linkGenerator.GetPathByAction(_httpContext, "Edit", "CalendarEvents", new
            {
                area = CalendarsConstants.Features.Calendars,
                id = occurrence.IsOverride ? calendarEvent.ItemId : occurrence.SeriesId,
                occurrence = occurrence.IsRecurring && !occurrence.IsOverride ? CalendarAdminEndpoints.FormatInstant(occurrence.OriginalStartUtc) : null,
            })
            : null;

        return new CalendarViewEvent
        {
            Id = occurrence.GetOccurrenceId(),
            EventId = calendarEvent.ItemId,
            CalendarId = calendarEvent.CalendarId,
            Title = occurrence.BusyOnly ? S["Busy"].Value : calendarEvent.Title,
            AllDay = calendarEvent.AllDay,
            Start = calendarEvent.AllDay ? occurrence.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : CalendarAdminEndpoints.FormatInstant(occurrence.StartUtc),
            End = calendarEvent.AllDay ? occurrence.End.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : CalendarAdminEndpoints.FormatInstant(occurrence.EndUtc),
            CategoryId = occurrence.BusyOnly ? null : calendarEvent.CategoryId,

            // An event's own color first, then its category's, its kind's and its calendar's.
            Color = occurrence.BusyOnly ? "#868e96" : calendarEvent.Color ?? category?.Color ?? eventType?.Color ?? calendar?.Color,
            Editable = canChange,
            Details = new CalendarViewEventDetails
            {
                BusyOnly = occurrence.BusyOnly,
                Calendar = calendar?.Name,
                Category = category?.Name,
                EventType = occurrence.BusyOnly || eventType is null ? null : _localizerFactory.Localize(eventType.DisplayName).Value,
                Location = occurrence.BusyOnly ? null : calendarEvent.Location,
                Description = occurrence.BusyOnly ? null : calendarEvent.Description,
                Status = calendarEvent.Status switch
                {
                    CalendarEventStatus.Tentative => S["Tentative"].Value,
                    CalendarEventStatus.Cancelled => S["Cancelled"].Value,
                    _ => S["Confirmed"].Value,
                },
                Recurring = occurrence.IsRecurring,
                OriginalStart = occurrence.IsRecurring ? CalendarAdminEndpoints.FormatInstant(occurrence.OriginalStartUtc) : null,
                Source = calendarEvent.Source,
                Attendees = occurrence.BusyOnly
                    ? []
                    : calendarEvent.Attendees.Select(attendee => attendee.Name ?? attendee.Email ?? attendee.ResourceCalendarId).Where(name => !string.IsNullOrEmpty(name)).ToList(),
                EditUrl = editUrl,
            },
        };
    }
}
