using System.Net.Mail;
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
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

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

        var existing = calendarEvent.Attendees.Where(attendee => attendee.GetKey() is not null).GroupBy(attendee => attendee.GetKey()).ToDictionary(group => group.Key, group => group.First());
        var attendees = new List<CalendarAttendee>();
        var users = await _userLookup.GetAsync(model.UserIds ?? []);

        foreach (var user in users)
        {
            var contact = await _userLookup.GetContactAsync(user.Value);

            attendees.Add(Keep(existing, new CalendarAttendee
            {
                UserId = user.Value,
                Name = contact?.Name ?? user.Text,
                Email = contact?.Email,
                Role = CalendarAttendeeRole.Required,
            }));
        }

        foreach (var line in (model.Guests ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (MailAddress.TryCreate(line, out var address))
            {
                attendees.Add(Keep(existing, new CalendarAttendee
                {
                    Email = address.Address,
                    Name = string.IsNullOrWhiteSpace(address.DisplayName) ? null : address.DisplayName,
                    Role = CalendarAttendeeRole.Required,
                }));
            }
            else
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Guests), S["'{0}' is not an email address.", line]);
            }
        }

        foreach (var resourceId in (model.ResourceIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            var resource = await _calendarService.FindByIdAsync(resourceId);

            attendees.Add(Keep(existing, new CalendarAttendee
            {
                ResourceCalendarId = resourceId,
                Name = resource?.Name,
                Role = CalendarAttendeeRole.Resource,
                Response = CalendarAttendeeResponse.Accepted,
            }));
        }

        calendarEvent.Attendees = attendees;

        return await EditAsync(calendarEvent, context);
    }

    // An attendee who was already invited keeps their answer.
    private static CalendarAttendee Keep(Dictionary<string, CalendarAttendee> existing, CalendarAttendee attendee)
    {
        if (existing.TryGetValue(attendee.GetKey(), out var earlier))
        {
            attendee.Response = earlier.Response;
            attendee.RespondedUtc = earlier.RespondedUtc;
            attendee.ResponseToken = earlier.ResponseToken;
            attendee.Role = earlier.Role == CalendarAttendeeRole.Organizer ? earlier.Role : attendee.Role;
        }

        return attendee;
    }
}
