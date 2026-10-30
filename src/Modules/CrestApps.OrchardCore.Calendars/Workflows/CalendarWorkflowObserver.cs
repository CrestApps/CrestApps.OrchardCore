using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Starts the calendar workflow events once a change is committed.
/// </summary>
internal sealed class CalendarWorkflowObserver : ICalendarEventObserver
{
    private readonly IWorkflowManager _workflowManager;

    public CalendarWorkflowObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task OnChangedAsync(CalendarEventChange change)
    {
        var name = change.Kind switch
        {
            CalendarEventChangeKind.Created => nameof(CalendarEventCreatedEvent),
            CalendarEventChangeKind.Updated or CalendarEventChangeKind.OccurrenceChanged => nameof(CalendarEventUpdatedEvent),
            CalendarEventChangeKind.Cancelled => nameof(CalendarEventCancelledEvent),
            CalendarEventChangeKind.Deleted => nameof(CalendarEventDeletedEvent),
            CalendarEventChangeKind.AttendeeAdded => nameof(CalendarAttendeeInvitedEvent),
            CalendarEventChangeKind.ResponseChanged => nameof(CalendarAttendeeRespondedEvent),
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        return _workflowManager.TriggerEventAsync(name, CalendarWorkflowInput.FromChange(change), change.Event.ItemId);
    }
}
