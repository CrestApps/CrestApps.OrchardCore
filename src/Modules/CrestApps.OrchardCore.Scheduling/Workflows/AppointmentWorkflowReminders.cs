using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Scheduling.Workflows;

/// <summary>
/// Raises Appointment Starting Soon from the Calendar Reminders feature for the calendar events appointments create.
/// </summary>
internal sealed class AppointmentWorkflowReminders : ICalendarReminderOffsetProvider, ICalendarReminderObserver
{
    private readonly IWorkflowTypeStore _workflowTypeStore;
    private readonly IWorkflowManager _workflowManager;
    private readonly AppointmentStore _appointmentStore;
    private readonly AppointmentTypeStore _typeStore;

    public AppointmentWorkflowReminders(
        IWorkflowTypeStore workflowTypeStore,
        IWorkflowManager workflowManager,
        AppointmentStore appointmentStore,
        AppointmentTypeStore typeStore)
    {
        _workflowTypeStore = workflowTypeStore;
        _workflowManager = workflowManager;
        _appointmentStore = appointmentStore;
        _typeStore = typeStore;
    }

    public async Task<IEnumerable<TimeSpan>> GetOffsetsAsync()
    {
        var offsets = new HashSet<TimeSpan>();

        foreach (var workflowType in await _workflowTypeStore.GetByStartActivityAsync(nameof(AppointmentStartingSoonEvent)))
        {
            foreach (var activity in workflowType.Activities.Where(activity => workflowType.IsEnabled && activity.Name == nameof(AppointmentStartingSoonEvent)))
            {
                var minutes = activity.Properties?["MinutesBefore"] is JsonValue value && value.TryGetValue<int>(out var parsed) ? parsed : 60;

                offsets.Add(TimeSpan.FromMinutes(Math.Max(0, minutes)));
            }
        }

        return offsets;
    }

    public async Task OnStartingSoonAsync(CalendarReminderContext context)
    {
        var calendarEvent = context.Occurrence.Event;

        if (calendarEvent.Source != SchedulingConstants.EventSource || string.IsNullOrEmpty(calendarEvent.SourceId) || calendarEvent.SourceId.StartsWith("session:", StringComparison.Ordinal))
        {
            return;
        }

        var appointment = await _appointmentStore.FindByIdAsync(calendarEvent.SourceId);

        if (appointment is null || appointment.Status is not (Models.AppointmentStatus.Confirmed or Models.AppointmentStatus.Requested))
        {
            return;
        }

        var input = AppointmentWorkflowInput.ToInput(appointment, await _typeStore.FindByIdAsync(appointment.AppointmentTypeId));

        input["MinutesBefore"] = (int)context.Offset.TotalMinutes;

        await _workflowManager.TriggerEventAsync(nameof(AppointmentStartingSoonEvent), input, appointment.ItemId);
    }

    public Task OnCompletedAsync(CalendarReminderContext context)
    {
        return Task.CompletedTask;
    }
}
