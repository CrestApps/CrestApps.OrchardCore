using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the calendar workflow events and tasks.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<CalendarWorkflowService>();
        services.AddScoped<ICalendarEventObserver, CalendarWorkflowObserver>();

        services.AddActivity<CalendarEventCreatedEvent, CalendarEventActivityDisplayDriver<CalendarEventCreatedEvent>>();
        services.AddActivity<CalendarEventUpdatedEvent, CalendarEventActivityDisplayDriver<CalendarEventUpdatedEvent>>();
        services.AddActivity<CalendarEventCancelledEvent, CalendarEventActivityDisplayDriver<CalendarEventCancelledEvent>>();
        services.AddActivity<CalendarEventDeletedEvent, CalendarEventActivityDisplayDriver<CalendarEventDeletedEvent>>();
        services.AddActivity<CalendarAttendeeInvitedEvent, CalendarEventActivityDisplayDriver<CalendarAttendeeInvitedEvent>>();
        services.AddActivity<CalendarAttendeeRespondedEvent, CalendarEventActivityDisplayDriver<CalendarAttendeeRespondedEvent>>();

        services.AddActivity<CreateCalendarEventTask, CalendarTaskDisplayDriver<CreateCalendarEventTask>>();
        services.AddActivity<UpdateCalendarEventTask, CalendarTaskDisplayDriver<UpdateCalendarEventTask>>();
        services.AddActivity<CancelCalendarEventTask, CalendarTaskDisplayDriver<CancelCalendarEventTask>>();
        services.AddActivity<GetCalendarEventTask, CalendarTaskDisplayDriver<GetCalendarEventTask>>();
        services.AddActivity<FindCalendarEventsTask, CalendarTaskDisplayDriver<FindCalendarEventsTask>>();
        services.AddActivity<AddCalendarAttendeeTask, CalendarTaskDisplayDriver<AddCalendarAttendeeTask>>();
        services.AddActivity<RemoveCalendarAttendeeTask, CalendarTaskDisplayDriver<RemoveCalendarAttendeeTask>>();
        services.AddActivity<FindFreeTimeTask, CalendarTaskDisplayDriver<FindFreeTimeTask>>();
    }
}
