using System.Globalization;
using System.Net;
using System.Text;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Calendars.Invitations;

/// <summary>
/// Emails attendees once a change is committed: an invitation with a calendar file when they are invited, an update when the
/// event's time, place or title changes, and a cancellation when it is cancelled or deleted.
/// </summary>
internal sealed class CalendarInvitationObserver : ICalendarEventObserver
{
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public CalendarInvitationObserver(
        IEmailService emailService,
        ISiteService siteService,
        IClock clock,
        ILogger<CalendarInvitationObserver> logger,
        IStringLocalizer<CalendarInvitationObserver> stringLocalizer)
    {
        _emailService = emailService;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task OnChangedAsync(CalendarEventChange change, CancellationToken cancellationToken = default)
    {
        if (!change.SendInvitations || change.Event is null)
        {
            return;
        }

        var calendarEvent = change.Event;
        IEnumerable<CalendarAttendee> recipients;
        string method;
        string subject;

        switch (change.Kind)
        {
            case CalendarEventChangeKind.AttendeeAdded when calendarEvent.Status != CalendarEventStatus.Cancelled:
                recipients = [change.Attendee];
                method = "REQUEST";
                subject = S["Invitation: {0}", calendarEvent.Title];
                break;

            case CalendarEventChangeKind.Updated or CalendarEventChangeKind.OccurrenceChanged when change.Previous is not null && Matters(change.Previous, calendarEvent):
                recipients = calendarEvent.Attendees.Where(attendee => change.Previous.Attendees.Any(earlier => earlier.GetKey() == attendee.GetKey()));
                method = "REQUEST";
                subject = S["Updated: {0}", calendarEvent.Title];
                break;

            case CalendarEventChangeKind.Cancelled or CalendarEventChangeKind.Deleted:
                recipients = calendarEvent.Attendees;
                method = "CANCEL";
                subject = S["Cancelled: {0}", calendarEvent.Title];
                break;

            default:
                return;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var baseUrl = site.BaseUrl?.TrimEnd('/');

        foreach (var attendee in recipients.Where(attendee => attendee is not null && !string.IsNullOrWhiteSpace(attendee.Email) && attendee.Role != CalendarAttendeeRole.Organizer && attendee.Response != CalendarAttendeeResponse.Declined))
        {
            var invite = method == "CANCEL" ? CloneCancelled(calendarEvent) : calendarEvent;
            var ics = ICalendarWriter.Write(change.Calendar?.Name, [invite], _clock.UtcNow, method);
            var message = new MailMessage
            {
                To = attendee.Email,
                Subject = subject,
                HtmlBody = BuildBody(calendarEvent, attendee, method, baseUrl),
            };

            message.Attachments.Add(new MailMessageAttachment
            {
                Filename = "invite.ics",
                Stream = new MemoryStream(Encoding.UTF8.GetBytes(ics)),
            });

            var result = await _emailService.SendAsync(message, cancellationToken: cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The invitation for the event '{EventId}' could not be emailed to an attendee.", calendarEvent.ItemId);
            }
        }
    }

    private string BuildBody(CalendarEvent calendarEvent, CalendarAttendee attendee, string method, string baseUrl)
    {
        var builder = new StringBuilder();
        var when = calendarEvent.AllDay
            ? calendarEvent.Start.ToString("D", CultureInfo.CurrentCulture)
            : $"{calendarEvent.Start.ToString("f", CultureInfo.CurrentCulture)} ({calendarEvent.TimeZoneId})";

        builder.Append("<p><strong>").Append(WebUtility.HtmlEncode(calendarEvent.Title)).Append("</strong></p>");
        builder.Append("<p>").Append(WebUtility.HtmlEncode(S["When: {0}", when])).Append("</p>");

        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            builder.Append("<p>").Append(WebUtility.HtmlEncode(S["Where: {0}", calendarEvent.Location])).Append("</p>");
        }

        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            builder.Append("<p>").Append(WebUtility.HtmlEncode(calendarEvent.Description).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</p>");
        }

        if (method == "CANCEL")
        {
            builder.Append("<p>").Append(WebUtility.HtmlEncode(S["This event has been cancelled."])).Append("</p>");
        }
        else if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(attendee.ResponseToken))
        {
            var link = $"{baseUrl}/calendars/respond/{calendarEvent.ItemId}/{attendee.ResponseToken}/";

            builder.Append("<p>")
                .Append($"<a href=\"{link}Accepted\">{WebUtility.HtmlEncode(S["Accept"])}</a> &middot; ")
                .Append($"<a href=\"{link}Tentative\">{WebUtility.HtmlEncode(S["Maybe"])}</a> &middot; ")
                .Append($"<a href=\"{link}Declined\">{WebUtility.HtmlEncode(S["Decline"])}</a>")
                .Append("</p>");
        }

        return builder.ToString();
    }

    private static bool Matters(CalendarEvent previous, CalendarEvent current)
    {
        return previous.StartUtc != current.StartUtc ||
            previous.EndUtc != current.EndUtc ||
            !string.Equals(previous.Title, current.Title, StringComparison.Ordinal) ||
            !string.Equals(previous.Location, current.Location, StringComparison.Ordinal) ||
            !string.Equals(previous.Recurrence?.Rule, current.Recurrence?.Rule, StringComparison.Ordinal);
    }

    private static CalendarEvent CloneCancelled(CalendarEvent calendarEvent)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(calendarEvent);
        var copy = System.Text.Json.JsonSerializer.Deserialize<CalendarEvent>(json);

        copy.Status = CalendarEventStatus.Cancelled;
        copy.Sequence++;

        return copy;
    }
}
