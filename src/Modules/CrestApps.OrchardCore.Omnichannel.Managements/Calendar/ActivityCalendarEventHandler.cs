using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Calendar;

/// <summary>
/// Keeps an activity at its calendar event's time when the event is moved on the calendar, and lets go of the activity when
/// its event is cancelled or deleted.
/// </summary>
internal sealed class ActivityCalendarEventHandler : CalendarEventHandlerBase
{
    // The activity manager runs the activity handlers, one of which changes calendar events, so it is reached lazily.
    private readonly Lazy<IOmnichannelActivityManager> _activityManager;

    public ActivityCalendarEventHandler(Lazy<IOmnichannelActivityManager> activityManager)
    {
        _activityManager = activityManager;
    }

    public override async Task SavedAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        var activity = await FindActivityAsync(context, cancellationToken);

        if (activity is null)
        {
            return;
        }

        if (context.Event.Status == CalendarEventStatus.Cancelled)
        {
            await UnlinkAsync(activity, cancellationToken);

            return;
        }

        // Only an activity still waiting to be worked moves; a finished one keeps the time it happened.
        if (activity.Status is not (ActivityStatus.NotStated or ActivityStatus.Scheduled) || activity.ScheduledUtc == context.Event.StartUtc)
        {
            return;
        }

        activity.ScheduledUtc = context.Event.StartUtc;

        await _activityManager.Value.UpdateAsync(activity, cancellationToken: cancellationToken);
    }

    public override async Task DeletingAsync(CalendarEventContext context, CancellationToken cancellationToken = default)
    {
        if (await FindActivityAsync(context, cancellationToken) is { } activity)
        {
            await UnlinkAsync(activity, cancellationToken);
        }
    }

    private async Task<OmnichannelActivity> FindActivityAsync(CalendarEventContext context, CancellationToken cancellationToken)
    {
        var calendarEvent = context.Event;

        // A change this feature made to follow its activity doesn't go back to the activity.
        if (calendarEvent is null ||
            !string.Equals(calendarEvent.Source, ActivityCalendarService.Source, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(calendarEvent.SourceId) ||
            string.Equals(context.Options?.Actor, ActivityCalendarService.Source, StringComparison.Ordinal))
        {
            return null;
        }

        var activity = await _activityManager.Value.FindByIdAsync(calendarEvent.SourceId, cancellationToken);

        return activity is not null && activity.TryGet<ActivityCalendarLink>(out var link) && link.EventId == calendarEvent.ItemId ? activity : null;
    }

    private Task UnlinkAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        activity.Put(new ActivityCalendarLink());

        return _activityManager.Value.UpdateAsync(activity, cancellationToken: cancellationToken).AsTask();
    }
}
