using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.Drivers;

/// <summary>
/// The Attendees card of the event editor: users, outside guests by email, and rooms or equipment.
/// </summary>
internal sealed class CalendarEventAttendeesDisplayDriver : DisplayDriver<CalendarEvent>
{
    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private readonly ICalendarService _calendarService;
    private readonly ICalendarTypeManager _typeManager;
    private readonly CalendarUserLookup _userLookup;

    internal readonly IStringLocalizer S;

    public CalendarEventAttendeesDisplayDriver(
        ICalendarService calendarService,
        ICalendarTypeManager typeManager,
        CalendarUserLookup userLookup,
        IStringLocalizer<CalendarEventAttendeesDisplayDriver> stringLocalizer)
    {
        _calendarService = calendarService;
        _typeManager = typeManager;
        _userLookup = userLookup;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(CalendarEvent calendarEvent, BuildEditorContext context)
    {
        var calendar = await _calendarService.FindByIdAsync(calendarEvent.CalendarId);

        if (calendar is null || !(_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.Attendees) ?? true))
        {
            return null;
        }

        var users = await _userLookup.GetAsync(calendarEvent.Attendees.Select(attendee => attendee.UserId));
        var resources = await _calendarService.ListAsync(new CalendarListQuery { Enabled = true, Limit = 500 });

        return Initialize<CalendarEventAttendeesViewModel>("CalendarEventAttendees_Edit", model =>
        {
            model.UserIds = calendarEvent.Attendees.Where(attendee => !string.IsNullOrEmpty(attendee.UserId)).Select(attendee => attendee.UserId).ToArray();
            model.InitialUsersJson = JsonSerializer.Serialize(users.Select(user => new { value = user.Value, text = user.Text }), _jsonOptions);
            model.Guests = string.Join(Environment.NewLine, calendarEvent.Attendees
                .Where(attendee => string.IsNullOrEmpty(attendee.UserId) && string.IsNullOrEmpty(attendee.ResourceCalendarId) && !string.IsNullOrEmpty(attendee.Email))
                .Select(attendee => string.IsNullOrWhiteSpace(attendee.Name) ? attendee.Email : $"{attendee.Name} <{attendee.Email}>"));
            model.ResourceIds = calendarEvent.Attendees.Where(attendee => !string.IsNullOrEmpty(attendee.ResourceCalendarId)).Select(attendee => attendee.ResourceCalendarId).ToArray();
            model.ResourceOptions = resources
                .Where(resource => resource.OwnerKind == CalendarOwnerKind.Resource && resource.ItemId != calendar.ItemId)
                .Select(resource => new SelectListItem(resource.Name, resource.ItemId, model.ResourceIds.Contains(resource.ItemId)))
                .ToList();
            model.Attendees = calendarEvent.Attendees.ToList();
        }).Location("Content:10%Attendees;3");
    }

    public override async Task<IDisplayResult> UpdateAsync(CalendarEvent calendarEvent, UpdateEditorContext context)
    {
        var calendar = await _calendarService.FindByIdAsync(calendarEvent.CalendarId);

        if (calendar is null || !(_typeManager.FindType(calendar.Type)?.Supports(CalendarCapabilities.Attendees) ?? true))
        {
            return null;
        }

        var model = new CalendarEventAttendeesViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        calendarEvent.Attendees = await CalendarAttendeeList.BuildAsync(
            calendarEvent.Attendees,
            model.UserIds,
            (model.Guests ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            model.ResourceIds,
            _userLookup,
            _calendarService,
            line => context.Updater.ModelState.AddModelError(Prefix, nameof(model.Guests), S["'{0}' is not an email address.", line]));

        return await EditAsync(calendarEvent, context);
    }
}
