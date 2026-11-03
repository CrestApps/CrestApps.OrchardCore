using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Places the follow-up activities the agent ticked "Add to my calendar" for on their owner's calendar.
/// </summary>
internal sealed class ActivityCalendarDispositionHandler : IActivityDispositionHandler
{
    private readonly ActivityCalendarService _calendarService;
    private readonly ISession _session;

    public ActivityCalendarDispositionHandler(ActivityCalendarService calendarService, ISession session)
    {
        _calendarService = calendarService;
        _session = session;
    }

    public async Task DispositionedAsync(ActivityDispositionRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.ActionsToCalendar is not { Count: > 0 } || request.CreatedActivities is not { Count: > 0 })
        {
            return;
        }

        foreach (var actionId in request.ActionsToCalendar)
        {
            if (!request.CreatedActivities.TryGetValue(actionId, out var followUp))
            {
                continue;
            }

            // The follow-up belongs to whoever it was assigned to, which is the agent unless the action named someone else.
            var userId = string.IsNullOrEmpty(followUp.AssignedToId) ? request.ActorId : followUp.AssignedToId;
            var userName = string.IsNullOrEmpty(followUp.AssignedToUsername) ? request.ActorDisplayName : followUp.AssignedToUsername;

            if (await _calendarService.PlaceAsync(followUp, userId, userName, cancellationToken) is not null)
            {
                // The follow-up was saved straight to the session, so the link is saved the same way.
                await _session.SaveAsync(followUp, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            }
        }
    }
}
