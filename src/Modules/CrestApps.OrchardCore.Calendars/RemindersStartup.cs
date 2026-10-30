using CrestApps.OrchardCore.Calendars.BackgroundTasks;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Migrations;
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
            .AddIndexProvider<CalendarReminderLogIndexProvider>();
        services.AddScoped<CalendarReminderService>();
        services.AddSingleton<IBackgroundTask, CalendarReminderBackgroundTask>();
    }
}
