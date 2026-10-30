using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers Appointment Starting Soon, raised by the Calendar Reminders feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows", CalendarsConstants.Features.Reminders)]
public sealed class WorkflowRemindersStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AppointmentStartingSoonEvent, AppointmentEventActivityDisplayDriver<AppointmentStartingSoonEvent>>();
        services.AddScoped<AppointmentWorkflowReminders>();
        services.AddScoped<ICalendarReminderOffsetProvider>(serviceProvider => serviceProvider.GetRequiredService<AppointmentWorkflowReminders>());
        services.AddScoped<ICalendarReminderObserver>(serviceProvider => serviceProvider.GetRequiredService<AppointmentWorkflowReminders>());
    }
}
