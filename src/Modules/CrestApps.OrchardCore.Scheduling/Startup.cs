using Microsoft.Extensions.DependencyInjection.Extensions;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Scheduling.BackgroundTasks;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Drivers;
using CrestApps.OrchardCore.Scheduling.Migrations;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Navigation;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling;

/// <summary>
/// Registers the scheduling services and screens, and the Appointments calendar type.
/// </summary>
/// <summary>
/// Picks the person behind a staff resource among staff, and fills the resource from their staff record.
/// </summary>
[RequireFeatures(CrestApps.OrchardCore.Staff.StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<Staff.SchedulingStaffOptions>(options => options.UseStaffPicker = true);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<CrestApps.Core.Services.ICatalogEntryHandler<SchedulingResource>, Staff.SchedulingResourceStaffHandler>());
        services.AddStaffCapability(Staff.SchedulingResourceStaffHandler.BookableCapability, global::OrchardCore.Localization.LocalizationSource.Create("Bookable", typeof(StaffStartup)), global::OrchardCore.Localization.LocalizationSource.Create("Takes appointments as a scheduling resource.", typeof(StaffStartup)), 30);
    }
}

public sealed class Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(SchedulingConstants.CollectionName));
        services.AddDataMigration<SchedulingMigrations>()
            .AddIndexProvider<SchedulingResourceIndexProvider>()
            .AddIndexProvider<AppointmentTypeIndexProvider>()
            .AddIndexProvider<AppointmentIndexProvider>()
            .AddIndexProvider<WaitlistEntryIndexProvider>();

        services.AddSchedulingCore();
        services.AddPersonalDataContributor<AppointmentPersonalDataContributor>();
        services.AddScoped<SchedulingResourceCalendars>();
        services.AddScoped<SchedulingPageService>();
        services.AddDisplayDriver<Appointment, AppointmentDisplayDriver>();
        services.AddDisplayDriver<AppointmentType, AppointmentTypeDisplayDriver>();
        services.AddDisplayDriver<SchedulingResource, SchedulingResourceDisplayDriver>();

        // The editors are built by drivers, so other modules add sections to them with drivers of their own.
        services.AddDisplayDriver<AppointmentType, AppointmentTypeFieldsDisplayDriver>();
        services.AddDisplayDriver<SchedulingResource, SchedulingResourceFieldsDisplayDriver>();
        services.AddDisplayDriver<WaitlistEntry, WaitlistEntryDisplayDriver>();
        services.AddPermissionProvider<SchedulingPermissionsProvider>();
        services.AddNavigationProvider<SchedulingAdminMenu>();
        services.AddSingleton<IBackgroundTask, AppointmentHoldsBackgroundTask>();

        services.AddCalendarType(SchedulingConstants.CalendarType, type => type
            .WithDisplayName(LocalizationSource.Create<Startup>("Appointments"))
            .WithDescription(LocalizationSource.Create<Startup>("The appointments of a scheduling resource. Scheduling creates and manages these calendars."))
            .WithIcon("fa-solid fa-calendar-check")
            .WithColor("#0b7285")
            .WithPosition(50)
            .ManagedByCode()
            .AddEventType("Appointment", LocalizationSource.Create<Startup>("Appointment"), e => e.Color("#0b7285"))
            .AddEventType("Blocked", LocalizationSource.Create<Startup>("Blocked time"), e => e.Color("#868e96")));
    }
}
