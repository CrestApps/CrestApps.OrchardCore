using System.Security.Cryptography;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Invitations;

/// <summary>
/// Gives every attendee with an email a secret so they can answer from the invitation's links.
/// </summary>
internal sealed class CalendarInvitationTokenHandler : CalendarEventHandlerBase
{
    public override Task SavingAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        foreach (var attendee in context.Event.Attendees)
        {
            if (!string.IsNullOrWhiteSpace(attendee.Email) && string.IsNullOrEmpty(attendee.ResponseToken))
            {
                attendee.ResponseToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            }
        }

        return Task.CompletedTask;
    }
}
