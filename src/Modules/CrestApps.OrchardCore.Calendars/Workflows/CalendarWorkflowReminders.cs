using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// Connects the Calendar Reminders feature to workflows: the offsets come from the Calendar Event Starting Soon events of the
/// workflow types, and each reminder starts the matching workflows.
/// </summary>
internal sealed class CalendarWorkflowReminders : ICalendarReminderOffsetProvider, ICalendarReminderObserver
{
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly IWorkflowManager _workflowManager;

    public CalendarWorkflowReminders(
        IWorkflowTypeStore workflowTypeStore,
        IWorkflowManager workflowManager)
    {
        _workflowTypeStore = workflowTypeStore;
        _workflowManager = workflowManager;
    }

    public async Task<IEnumerable<TimeSpan>> GetOffsetsAsync()
    {
        var offsets = new HashSet<TimeSpan>();

        foreach (var workflowType in await _workflowTypeStore.GetByStartActivityAsync(nameof(CalendarEventStartingSoonEvent)))
        {
            if (!workflowType.IsEnabled)
            {
                continue;
            }

            foreach (var activity in workflowType.Activities.Where(activity => activity.Name == nameof(CalendarEventStartingSoonEvent)))
            {
                var minutes = activity.Properties?[nameof(CalendarEventStartingSoonEvent.MinutesBefore)] is JsonValue value && value.TryGetValue<int>(out var parsed)
                    ? parsed
                    : 15;

                offsets.Add(TimeSpan.FromMinutes(Math.Max(0, minutes)));
            }
        }

        return offsets;
    }

    public Task OnStartingSoonAsync(CalendarReminderContext context)
    {
        return _workflowManager.TriggerEventAsync(nameof(CalendarEventStartingSoonEvent), CalendarWorkflowInput.FromReminder(context), context.Occurrence.Event.ItemId);
    }

    public Task OnCompletedAsync(CalendarReminderContext context)
    {
        return _workflowManager.TriggerEventAsync(nameof(CalendarEventCompletedEvent), CalendarWorkflowInput.FromReminder(context), context.Occurrence.Event.ItemId);
    }
}
