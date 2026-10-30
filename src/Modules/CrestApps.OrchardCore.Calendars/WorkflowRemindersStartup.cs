using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the workflow events raised by calendar reminders.
/// </summary>
[Feature(CalendarsConstants.Features.Reminders)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowRemindersStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<CalendarEventStartingSoonEvent, CalendarEventActivityDisplayDriver<CalendarEventStartingSoonEvent>>();
        services.AddActivity<CalendarEventCompletedEvent, CalendarEventActivityDisplayDriver<CalendarEventCompletedEvent>>();
        services.AddScoped<CalendarWorkflowReminders>();
        services.AddScoped<ICalendarReminderOffsetProvider>(serviceProvider => serviceProvider.GetRequiredService<CalendarWorkflowReminders>());
        services.AddScoped<ICalendarReminderObserver>(serviceProvider => serviceProvider.GetRequiredService<CalendarWorkflowReminders>());
    }
}
