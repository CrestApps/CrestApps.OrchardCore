using System.Net;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Calendars.Invitations;

/// <summary>
/// The links in an invitation email: an outside attendee answers without signing in, proven by the secret in the link.
/// Opening a link only asks; the answer is recorded when the attendee confirms it, because mail scanners open every link
/// in a message and would otherwise accept or decline for the attendee.
/// </summary>
internal static class CalendarResponseEndpoint
{
    public static IEndpointRouteBuilder MapCalendarResponseEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("calendars/respond/{eventId}/{token}/{response}", ConfirmAsync)
            .AllowAnonymous()
            .WithName("CalendarsRespond");

        // The secret in the address is what authorizes the answer; the page posts back to the same address.
        routes.MapPost("calendars/respond/{eventId}/{token}/{response}", RespondAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithName("CalendarsRespondConfirm");

        return routes;
    }

    /// <summary>
    /// Shows the attendee what they are about to answer, with a button that records it. Nothing changes.
    /// </summary>
    internal static async Task<IResult> ConfirmAsync(
        string eventId,
        string token,
        string response,
        ICalendarEventManager eventManager,
        IStringLocalizer<CalendarInvitationObserver> S)
    {
        var (calendarEvent, attendee, answer) = await FindAsync(eventId, token, response, eventManager);

        if (attendee is null)
        {
            return Results.NotFound();
        }

        var question = answer switch
        {
            CalendarAttendeeResponse.Accepted => S["Accept the invitation to '{0}'?", calendarEvent.Title],
            CalendarAttendeeResponse.Tentative => S["Answer maybe to the invitation to '{0}'?", calendarEvent.Title],
            _ => S["Decline the invitation to '{0}'?", calendarEvent.Title],
        };
        var button = answer switch
        {
            CalendarAttendeeResponse.Accepted => S["Accept"],
            CalendarAttendeeResponse.Tentative => S["Maybe"],
            _ => S["Decline"],
        };

        return Page(calendarEvent.Title, $"<p>{WebUtility.HtmlEncode(question)}</p><form method=\"post\"><button type=\"submit\">{WebUtility.HtmlEncode(button)}</button></form>");
    }

    /// <summary>
    /// Records the answer the attendee confirmed.
    /// </summary>
    internal static async Task<IResult> RespondAsync(
        string eventId,
        string token,
        string response,
        ICalendarEventManager eventManager,
        IStringLocalizer<CalendarInvitationObserver> S)
    {
        var (calendarEvent, attendee, answer) = await FindAsync(eventId, token, response, eventManager);

        if (attendee is null)
        {
            return Results.NotFound();
        }

        var result = await eventManager.RespondAsync(calendarEvent, attendee.GetKey(), answer, new CalendarChangeOptions { Actor = attendee.Email, SendInvitations = false });
        var text = result.Succeeded ? S["Thank you. Your answer to '{0}' has been saved.", calendarEvent.Title] : S["Your answer could not be saved."];

        return Page(calendarEvent.Title, $"<p>{WebUtility.HtmlEncode(text)}</p>");
    }

    // The code in an invitation's links names one attendee of one event; it's compared in constant time so it can't be
    // guessed a character at a time.
    internal static CalendarAttendee FindAttendee(CalendarEvent calendarEvent, string token)
    {
        if (calendarEvent is null || string.IsNullOrEmpty(token))
        {
            return null;
        }

        var given = Encoding.UTF8.GetBytes(token);

        return calendarEvent.Attendees.FirstOrDefault(item => !string.IsNullOrEmpty(item.ResponseToken) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(item.ResponseToken), given));
    }

    private static async Task<(CalendarEvent Event, CalendarAttendee Attendee, CalendarAttendeeResponse Answer)> FindAsync(string eventId, string token, string response, ICalendarEventManager eventManager)
    {
        if (!Enum.TryParse<CalendarAttendeeResponse>(response, ignoreCase: true, out var answer) || answer == CalendarAttendeeResponse.NeedsAction || !Enum.IsDefined(answer))
        {
            return (null, null, answer);
        }

        var calendarEvent = await eventManager.FindByIdAsync(eventId);

        return (calendarEvent, FindAttendee(calendarEvent, token), answer);
    }

    private static IResult Page(string title, string body)
        => Results.Content($"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>{WebUtility.HtmlEncode(title)}</title></head><body>{body}</body></html>", "text/html");
}
