using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.HealthChecks;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers external calendar subscriptions, the iCalendar address provider, the synchronization task and its health check.
/// </summary>
[Feature(CalendarsConstants.Features.External)]
public sealed class ExternalStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(ICalendarUrlProvider));
        services.AddCalendarProvider<ICalendarUrlProvider>();
        services.AddSingleton<IBackgroundTask, CalendarSyncBackgroundTask>();
        services.AddDisplayDriver<Calendar, CalendarSubscriptionDisplayDriver>();
        services.AddHealthChecks().AddCheck<CalendarSyncHealthCheck>(CalendarSyncHealthCheck.Name, tags: ["calendars"]);
    }
}
