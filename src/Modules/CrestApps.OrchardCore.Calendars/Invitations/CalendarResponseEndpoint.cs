using System.Net;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Invitations;

/// <summary>
/// The links in an invitation email: an outside attendee answers without signing in, proven by the secret in the link.
/// </summary>
internal static class CalendarResponseEndpoint
{
    public static IEndpointRouteBuilder MapCalendarResponseEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("calendars/respond/{eventId}/{token}/{response}", RespondAsync)
            .AllowAnonymous()
            .WithName("CalendarsRespond");

        return routes;
    }

    private static async Task<IResult> RespondAsync(
        string eventId,
        string token,
        string response,
        ICalendarEventManager eventManager,
        IStringLocalizer<CalendarInvitationObserver> S)
    {
        var calendarEvent = await eventManager.FindByIdAsync(eventId);
        var attendee = calendarEvent?.Attendees.FirstOrDefault(item => !string.IsNullOrEmpty(item.ResponseToken) && string.Equals(item.ResponseToken, token, StringComparison.Ordinal));

        if (attendee is null || !Enum.TryParse<CalendarAttendeeResponse>(response, ignoreCase: true, out var answer) || answer == CalendarAttendeeResponse.NeedsAction)
        {
            return Results.NotFound();
        }

        var result = await eventManager.RespondAsync(calendarEvent, attendee.GetKey(), answer, new CalendarChangeOptions { Actor = attendee.Email, SendInvitations = false });
        var text = result.Succeeded ? S["Thank you. Your answer to '{0}' has been saved.", calendarEvent.Title] : S["Your answer could not be saved."];

        return Results.Content($"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(calendarEvent.Title)}</title></head><body><p>{WebUtility.HtmlEncode(text)}</p></body></html>", "text/html");
    }
}
