using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.Endpoints;
using CrestApps.OrchardCore.Calendars.Migrations;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Navigation;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the calendar services, screens and the General and Personal calendar types.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // Calendars live in their own YesSql collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CalendarsConstants.CollectionName));
        services.AddDataMigration<CalendarMigrations>()
            .AddIndexProvider<CalendarIndexProvider>()
            .AddIndexProvider<CalendarEventIndexProvider>();

        services.AddCalendarsCore();
        services.AddScoped<CalendarEventEditor>();
        services.AddScoped<CalendarUserLookup>();
        // The handler reaches these lazily: the access service authorizes, so a direct dependency would be a cycle.
        services.TryAddScoped(sp => new Lazy<IAuthorizationService>(sp.GetRequiredService<IAuthorizationService>));
        services.TryAddScoped(sp => new Lazy<ICalendarService>(sp.GetRequiredService<ICalendarService>));
        services.TryAddScoped(sp => new Lazy<ICalendarAccessService>(sp.GetRequiredService<ICalendarAccessService>));
        services.AddScoped<IAuthorizationHandler, CalendarEventAuthorizationHandler>();
        services.AddPermissionProvider<CalendarPermissionsProvider>();
        services.AddNavigationProvider<CalendarsAdminMenu>();
        services.AddResourceConfiguration<CalendarResourceConfiguration>();

        services.AddDisplayDriver<Calendar, CalendarDisplayDriver>();
        services.AddDisplayDriver<Calendar, CalendarOwnerDisplayDriver>();
        services.AddDisplayDriver<Calendar, CalendarSharingDisplayDriver>();
        services.AddDisplayDriver<Calendar, CalendarRulesDisplayDriver>();
        services.AddDisplayDriver<CalendarEvent, CalendarEventDisplayDriver>();
        services.AddDisplayDriver<CalendarEvent, CalendarEventRecurrenceDisplayDriver>();
        services.AddDisplayDriver<CalendarEvent, CalendarEventAttendeesDisplayDriver>();

        services.AddCalendarType(CalendarsConstants.Types.General, type => type
            .WithDisplayName(LocalizationSource.Create<Startup>("General"))
            .WithDescription(LocalizationSource.Create<Startup>("A shared calendar that belongs to nobody in particular, such as a company or project calendar."))
            .WithIcon("fa-solid fa-calendar")
            .WithColor("#1971c2")
            .WithPosition(0)
            .AddEventType(CalendarsConstants.EventTypes.Default, LocalizationSource.Create<Startup>("Event"))
            .AddEventType("Meeting", LocalizationSource.Create<Startup>("Meeting"), e => e.Color("#1971c2").Lasts(60))
            .AddEventType("Holiday", LocalizationSource.Create<Startup>("Holiday"), e => e.Color("#e03131").AllDayByDefault().Free())
            .AddEventType("Reminder", LocalizationSource.Create<Startup>("Reminder"), e => e.Color("#868e96").Lasts(15).Free()));

        services.AddCalendarType(CalendarsConstants.Types.Personal, type => type
            .WithDisplayName(LocalizationSource.Create<Startup>("Personal"))
            .WithDescription(LocalizationSource.Create<Startup>("A user's own calendar. Others see when it is busy, not what is on it, unless the owner shares it."))
            .WithIcon("fa-solid fa-user")
            .WithColor("#2f9e44")
            .WithOwner(CalendarOwnerKind.User, onePerOwner: true)
            .WithVisibility(CalendarVisibility.Shared)
            .WithPosition(1)
            .AddEventType(CalendarsConstants.EventTypes.Default, LocalizationSource.Create<Startup>("Event"))
            .AddEventType("Meeting", LocalizationSource.Create<Startup>("Meeting"), e => e.Color("#1971c2"))
            .AddEventType("Focus", LocalizationSource.Create<Startup>("Focus time"), e => e.Color("#7048e8"))
            .AddEventType("OutOfOffice", LocalizationSource.Create<Startup>("Out of office"), e => e.Color("#e8590c").AllDayByDefault()));
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapCalendarAdminEndpoints();
    }
}
