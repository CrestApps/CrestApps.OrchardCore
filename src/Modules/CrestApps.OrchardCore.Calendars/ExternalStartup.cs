using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Core.ICalendar;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers external calendar subscriptions, the iCalendar address provider and the synchronization task.
/// </summary>
[Feature(CalendarsConstants.Features.External)]
public sealed class ExternalStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(nameof(ICalendarUrlProvider));
        services.TryAddScoped<ICalendarImporter>();
        services.AddScoped<ICalendarProvider, ICalendarUrlProvider>();
        services.AddScoped<CalendarSyncService>();
        services.AddSingleton<IBackgroundTask, CalendarSyncBackgroundTask>();
        services.AddDisplayDriver<Calendar, CalendarSubscriptionDisplayDriver>();
    }
}
