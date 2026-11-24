using System.Net.Mail;
using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Services;

/// <summary>
/// Builds an event's attendee list the way the event editor's Attendees card does: users of the site, outside guests by
/// email and rooms or equipment, each attendee who was already invited keeping their answer.
/// </summary>
internal static class CalendarAttendeeList
{
    /// <summary>
    /// Builds the attendee list.
    /// </summary>
    /// <param name="current">The event's attendees now.</param>
    /// <param name="userIds">The IDs of the users to invite. Unknown IDs are left out.</param>
    /// <param name="guests">The outside guests, one email address (with an optional display name) per item.</param>
    /// <param name="resourceIds">The IDs of the room and equipment calendars to book.</param>
    /// <param name="userLookup">The user lookup.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="invalidGuest">Called with each guest that is not an email address.</param>
    /// <returns>The attendees.</returns>
    public static async Task<List<CalendarAttendee>> BuildAsync(
        IEnumerable<CalendarAttendee> current,
        IEnumerable<string> userIds,
        IEnumerable<string> guests,
        IEnumerable<string> resourceIds,
        CalendarUserLookup userLookup,
        ICalendarService calendarService,
        Action<string> invalidGuest)
    {
        ArgumentNullException.ThrowIfNull(userLookup);
        ArgumentNullException.ThrowIfNull(calendarService);

        var existing = (current ?? []).Where(attendee => attendee.GetKey() is not null).GroupBy(attendee => attendee.GetKey()).ToDictionary(group => group.Key, group => group.First());
        var attendees = new List<CalendarAttendee>();
        var users = await userLookup.GetAsync(userIds ?? []);

        foreach (var user in users)
        {
            var contact = await userLookup.GetContactAsync(user.Value);

            attendees.Add(Keep(existing, new CalendarAttendee
            {
                UserId = user.Value,
                Name = contact?.Name ?? user.Text,
                Email = contact?.Email,
                Role = CalendarAttendeeRole.Required,
            }));
        }

        foreach (var line in guests ?? [])
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
                invalidGuest?.Invoke(line);
            }
        }

        foreach (var resourceId in (resourceIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            var resource = await calendarService.FindByIdAsync(resourceId);

            attendees.Add(Keep(existing, new CalendarAttendee
            {
                ResourceCalendarId = resourceId,
                Name = resource?.Name,
                Role = CalendarAttendeeRole.Resource,
                Response = CalendarAttendeeResponse.Accepted,
            }));
        }

        return attendees;
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
