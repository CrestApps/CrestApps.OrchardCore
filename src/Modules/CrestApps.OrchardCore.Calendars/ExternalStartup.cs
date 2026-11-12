using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.HealthChecks;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Core.Http;
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
        // The address is a calendar manager's, so the client never reaches the site's own network or follows a redirect,
        // whatever the host resolves to when the sync task calls it.
        services.AddEgressGuardedHttpClient(nameof(ICalendarUrlProvider), client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddCalendarProvider<ICalendarUrlProvider>();
        services.AddSingleton<IBackgroundTask, CalendarSyncBackgroundTask>();
        services.AddDisplayDriver<Calendar, CalendarSubscriptionDisplayDriver>();
        services.AddHealthChecks().AddCheck<CalendarSyncHealthCheck>(CalendarSyncHealthCheck.Name, tags: ["calendars"]);
    }
}
