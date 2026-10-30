using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The General card of the event editor: title, type, time, place and status.
/// </summary>
internal sealed class CalendarEventDisplayDriver : DisplayDriver<CalendarEvent>
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CalendarEventDisplayDriver(
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        IStringLocalizerFactory localizerFactory,
        IClock clock,
        IStringLocalizer<CalendarEventDisplayDriver> stringLocalizer)
    {
        _calendarService = calendarService;
        _typeManager = typeManager;
        _localizerFactory = localizerFactory;
        _clock = clock;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(CalendarEvent calendarEvent, BuildEditorContext context)
    {
        var calendar = await _calendarService.FindByIdAsync(calendarEvent.CalendarId);
        var type = calendar is null ? null : _typeManager.FindType(calendar.Type);
        var eventTypes = calendar is null ? [] : _typeManager.ListEventTypes(calendar.Type);

        return Initialize<CalendarEventGeneralViewModel>("CalendarEventGeneral_Edit", model =>
        {
            var lastDay = calendarEvent.AllDay ? calendarEvent.End.Date.AddDays(-1) : calendarEvent.End;

            if (lastDay < calendarEvent.Start.Date)
            {
                lastDay = calendarEvent.Start.Date;
            }

            model.Title = calendarEvent.Title;
            model.EventType = calendarEvent.EventType;
            model.AllDay = calendarEvent.AllDay;
            model.StartDate = calendarEvent.Start.ToString(DateFormat, CultureInfo.InvariantCulture);
            model.StartTime = calendarEvent.Start.ToString(TimeFormat, CultureInfo.InvariantCulture);
            model.EndDate = lastDay.ToString(DateFormat, CultureInfo.InvariantCulture);
            model.EndTime = calendarEvent.AllDay ? "10:00" : calendarEvent.End.ToString(TimeFormat, CultureInfo.InvariantCulture);
            model.TimeZoneId = calendarEvent.TimeZoneId;
            model.Location = calendarEvent.Location;
            model.Description = calendarEvent.Description;
            model.Status = calendarEvent.Status.ToString();
            model.Transparency = calendarEvent.Transparency.ToString();
            model.Visibility = calendarEvent.Visibility.ToString();
            model.UseColor = !string.IsNullOrEmpty(calendarEvent.Color);
            model.Color = calendarEvent.Color ?? _typeManager.FindEventType(calendar?.Type, calendarEvent.EventType)?.Color ?? calendar?.Color ?? "#1971c2";
            model.CalendarName = calendar?.Name;
            model.SupportsAllDay = type?.Supports(CalendarCapabilities.AllDay) ?? true;
            model.EventTypeOptions = eventTypes
                .Select(eventType => new SelectListItem(_localizerFactory.Localize(eventType.DisplayName).Value, eventType.Name, string.Equals(eventType.Name, calendarEvent.EventType, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            model.TimeZoneOptions = CalendarSelectLists.TimeZones(_clock, calendarEvent.TimeZoneId);
            model.StatusOptions = CalendarSelectLists.Statuses(S);
            model.TransparencyOptions = CalendarSelectLists.Transparencies(S);
            model.VisibilityOptions = CalendarSelectLists.EventVisibilities(S);
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(CalendarEvent calendarEvent, UpdateEditorContext context)
    {
        var model = new CalendarEventGeneralViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendarEvent.Title = model.Title?.Trim();
        calendarEvent.EventType = model.EventType;
        calendarEvent.AllDay = model.AllDay;
        calendarEvent.Location = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim();
        calendarEvent.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        calendarEvent.Color = model.UseColor && !string.IsNullOrWhiteSpace(model.Color) ? model.Color.Trim() : null;

        if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
        {
            calendarEvent.TimeZoneId = CalendarTime.NormalizeZoneId(model.TimeZoneId) ?? model.TimeZoneId.Trim();
        }

        if (Enum.TryParse<CalendarEventStatus>(model.Status, out var status))
        {
            calendarEvent.Status = status;
        }

        if (Enum.TryParse<CalendarTransparency>(model.Transparency, out var transparency))
        {
            calendarEvent.Transparency = transparency;
        }

        if (Enum.TryParse<CalendarEventVisibility>(model.Visibility, out var visibility))
        {
            calendarEvent.Visibility = visibility;
        }

        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Title), S["The title is required."]);
        }

        if (!DateTime.TryParseExact(model.StartDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StartDate), S["Enter the date the event starts."]);
        }
        else if (!DateTime.TryParseExact(model.EndDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.EndDate), S["Enter the date the event ends."]);
        }
        else if (model.AllDay)
        {
            calendarEvent.Start = startDate;
            calendarEvent.End = (endDate < startDate ? startDate : endDate).AddDays(1);
        }
        else if (!TimeSpan.TryParseExact(model.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var startTime) ||
            !TimeSpan.TryParseExact(model.EndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var endTime))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.StartTime), S["Enter the times the event starts and ends."]);
        }
        else
        {
            calendarEvent.Start = startDate + startTime;
            calendarEvent.End = endDate + endTime;

            if (calendarEvent.End <= calendarEvent.Start)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.EndTime), S["The event must end after it starts."]);
            }
        }

        return await EditAsync(calendarEvent, context);
    }
}
