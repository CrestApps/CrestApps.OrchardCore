using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Filters;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the reminder ledger and the background task that raises reminders.
/// </summary>
[Feature(CalendarsConstants.Features.Reminders)]
public sealed class RemindersStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<CalendarReminderMigrations>()
            .AddIndexProvider<CalendarReminderLogIndexProvider>()
            .AddIndexProvider<CalendarUserReminderIndexProvider>();
        services.AddScoped<CalendarReminderService>();

        // An event's own reminders show on its people's screens until they dismiss them.
        services.AddScoped<CalendarUserReminderStore>();
        services.AddScoped<ICalendarReminderObserver, CalendarUserReminderObserver>();
        services.Configure<MvcOptions>(options => options.Filters.Add<CalendarRemindersFilter>());
        services.AddSingleton<IBackgroundTask, CalendarReminderBackgroundTask>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCalendarReminderEndpoints();
    }
}
