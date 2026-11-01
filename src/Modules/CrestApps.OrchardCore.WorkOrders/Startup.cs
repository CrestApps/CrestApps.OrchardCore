using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders.BackgroundTasks;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Drivers;
using CrestApps.OrchardCore.WorkOrders.Integrations;
using CrestApps.OrchardCore.WorkOrders.Migrations;
using CrestApps.OrchardCore.WorkOrders.Navigation;
using CrestApps.OrchardCore.WorkOrders.Services;
using CrestApps.OrchardCore.WorkOrders.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(WorkOrdersConstants.CollectionName));
        services.AddWorkOrdersCore();
        services.AddPermissionProvider<WorkOrdersPermissionsProvider>();
        services
            .AddIndexProvider<WorkOrderIndexProvider>()
            .AddIndexProvider<WorkOrderTypeIndexProvider>()
            .AddIndexProvider<ChecklistTemplateIndexProvider>()
            .AddIndexProvider<ServiceLevelPolicyIndexProvider>()
            .AddIndexProvider<WorkOrderSubStatusIndexProvider>()
            .AddDataMigration<WorkOrdersMigrations>();

        services.AddDisplayDriver<WorkOrderRow, WorkOrderRowDisplayDriver>();
        services.AddDisplayDriver<WorkOrderSettingRow, WorkOrderSettingRowDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, SlaMonitorBackgroundTask>());
        services.AddScoped<WorkOrderLookups>();
        services.AddNavigationProvider<WorkOrdersAdminMenu>();
    }
}

/// <summary>
/// Takes parts used off Inventory locations.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Inventory")]
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
/// Lets work orders be for assets.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Assets")]
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
[RequireFeatures("CrestApps.OrchardCore.Transactions")]
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
[RequireFeatures("CrestApps.OrchardCore.Geo")]
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
[RequireFeatures("OrchardCore.Workflows")]
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
