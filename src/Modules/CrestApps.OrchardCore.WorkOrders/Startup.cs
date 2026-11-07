using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.BusinessHours.Core;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Parties;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.WorkOrders.BackgroundTasks;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Drivers;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Migrations;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Navigation;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.WorkOrders;

/// <summary>
/// Registers work orders, their settings records, the service level monitor and the pages.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPartyDirectory();
        services.AddScoped<Integrations.WorkOrderCustomers>();
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WorkOrdersConstants.CollectionName));
        services.AddWorkOrdersCore();

        // A service level policy's clock runs in the hours of the calendar it names, read through the shared business
        // hours, so any feature that keeps calendars (the Contact Center business hours) feeds it.
        services.AddOpenHours();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IServiceHoursProvider, OpenHoursServiceHoursProvider>());
        services.AddPermissionProvider<WorkOrdersPermissionsProvider>();
        services
            .AddIndexProvider<WorkOrderIndexProvider>()
            .AddIndexProvider<WorkOrderTypeIndexProvider>()
            .AddIndexProvider<ChecklistTemplateIndexProvider>()
            .AddIndexProvider<ServiceLevelPolicyIndexProvider>()
            .AddIndexProvider<WorkOrderSubStatusIndexProvider>()
            .AddDataMigration<WorkOrdersMigrations>();

        services.AddDisplayDriver<WorkOrderRow, WorkOrderRowDisplayDriver>();
        services.AddDisplayDriver<WorkOrder, WorkOrderDisplayDriver>();
        services.AddDisplayDriver<WorkOrderSettingRow, WorkOrderSettingRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SlaMonitorBackgroundTask>());
        services.AddScoped<WorkOrderLookups>();
        services.AddNavigationProvider<WorkOrdersAdminMenu>();
    }

    /// <inheritdoc/>
    public override void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder app, Microsoft.AspNetCore.Routing.IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        Integrations.WorkOrderCustomerEndpoints.AddWorkOrderCustomerEndpoints(routes);
    }
}

/// <summary>
/// Takes parts used off Inventory locations.
/// </summary>
[RequireFeatures(InventoryConstants.Features.Inventory)]
public sealed class InventoryStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderPartsService, InventoryWorkOrderParts>();
        services.AddScoped<IWorkOrderLookup, InventoryWorkOrderLookup>();
    }
}

/// <summary>
/// Lets work order types need skills and certifications.
/// </summary>
[RequireFeatures(SkillsConstants.Features.Skills)]
public sealed class SkillsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderLookup, SkillsWorkOrderLookup>();
    }
}

/// <summary>
/// Lets work orders be for assets.
/// </summary>
[RequireFeatures(AssetsConstants.Features.Assets)]
public sealed class AssetsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderLookup, AssetsWorkOrderLookup>();
    }
}

/// <summary>
/// Invoices completed work orders through Transactions.
/// </summary>
[RequireFeatures(TransactionsConstants.Features.Area)]
public sealed class TransactionsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderBilling, TransactionsWorkOrderBilling>();
    }
}

/// <summary>
/// Gives work orders the territory of their service address and geocodes it.
/// </summary>
[RequireFeatures(GeoConstants.Features.Geo)]
public sealed class GeoStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped(sp => new Lazy<IGeocodeQueue>(sp.GetRequiredService<IGeocodeQueue>));
        services.AddScoped<IWorkOrderHandler, GeoWorkOrderHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGeocodeTarget, WorkOrderAddressGeocodeTarget>());
    }
}

/// <summary>
/// Registers the work order workflow events.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderObserver, WorkflowWorkOrderObserver>();
        services.AddActivity<WorkOrderCreatedEvent, WorkOrderEventActivityDisplayDriver<WorkOrderCreatedEvent>>();
        services.AddActivity<WorkOrderStatusChangedEvent, WorkOrderEventActivityDisplayDriver<WorkOrderStatusChangedEvent>>();
        services.AddActivity<WorkOrderSlaWarningEvent, WorkOrderEventActivityDisplayDriver<WorkOrderSlaWarningEvent>>();
        services.AddActivity<WorkOrderSlaBreachedEvent, WorkOrderEventActivityDisplayDriver<WorkOrderSlaBreachedEvent>>();
        services.AddActivity<WorkOrderCompletedEvent, WorkOrderEventActivityDisplayDriver<WorkOrderCompletedEvent>>();
    }
}

/// <summary>
/// Adds the Create work order subject action to Omnichannel dispositions.
/// </summary>
[RequireFeatures(OmnichannelConstants.Features.Managements)]
public sealed class OmnichannelStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelStartup(IStringLocalizer<OmnichannelStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<global::CrestApps.OrchardCore.Omnichannel.Core.Services.ISubjectActionHandler, Integrations.CreateWorkOrderSubjectActionHandler>();
        services.AddScoped<global::OrchardCore.ContentManagement.Display.ContentDisplay.IContentDisplayDriver, Integrations.ContactWorkOrdersDisplayDriver>();
        services.AddDisplayDriver<global::CrestApps.OrchardCore.Omnichannel.Core.Models.SubjectAction, Integrations.CreateWorkOrderSubjectActionDisplayDriver>();
        services.Configure<global::CrestApps.OrchardCore.Omnichannel.Core.Models.SubjectActionOptions>(options =>
        {
            options.AddActionType(Integrations.WorkOrderSubjectActions.CreateWorkOrder, entry =>
            {
                entry.DisplayName = S["Create Work Order"];
                entry.Description = S["Makes a work order of the chosen type for the activity's contact, with the activity's notes, once per activity."];
            });
        });
    }
}

/// <summary>
/// Shows an account's work orders on its page, and lets a work order take the account of its contact or customer.
/// </summary>
[RequireFeatures(AccountsConstants.Feature)]
public sealed class AccountsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<global::OrchardCore.ContentManagement.Display.ContentDisplay.IContentDisplayDriver, Integrations.AccountWorkOrdersDisplayDriver>();
    }
}

/// <summary>
/// Makes work orders from Scheduling bookings of the appointment types a work order type names.
/// </summary>
[RequireFeatures(SchedulingConstants.Features.Scheduling)]
public sealed class SchedulingStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IWorkOrderLookup, Integrations.SchedulingWorkOrderLookup>();
        services.AddScoped<global::CrestApps.OrchardCore.Scheduling.Services.IAppointmentObserver, Integrations.AppointmentWorkOrderObserver>();
    }
}
