using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Drivers;
using CrestApps.OrchardCore.Dispatch.Migrations;
using CrestApps.OrchardCore.Dispatch.Navigation;
using CrestApps.OrchardCore.Dispatch.RealTime;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Dispatch.ViewModels;
using CrestApps.OrchardCore.Dispatch.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Registers visits, the candidate finder, the dispatch service, the board and the settings.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(DispatchConstants.CollectionName));
        services.AddDispatchCore();
        services.TryAddScoped<IDispatchSettingsProvider, SiteDispatchSettingsProvider>();
        services.TryAddScoped<DispatchTimeZone>();
        services.AddPermissionProvider<DispatchPermissionsProvider>();
        services
            .AddIndexProvider<ServiceVisitIndexProvider>()
            .AddDataMigration<DispatchMigrations>();

        services.AddSiteDisplayDriver<DispatchSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(DispatchConstants.SettingsGroupId, DispatchPermissions.ManageDispatchSettings);
        services.AddDisplayDriver<VisitRow, VisitRowDisplayDriver>();
        services.AddDisplayDriver<WorkOrders.Models.WorkOrderPanels, WorkOrderVisitsDisplayDriver>();
        services.AddNavigationProvider<DispatchAdminMenu>();
        services.AddScoped<Geo.Services.ITerritoryMemberSource, WorkerTerritoryMemberSource>();
        services.AddScoped<Geo.Services.ITerritoryMemberSource, TeamTerritoryMemberSource>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, DispatchResourceConfiguration>();
        services.AddScoped<Core.Services.IDispatchSiteInfo, Integrations.SiteDispatchSiteInfo>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, Integrations.VisitRiskBackgroundTask>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, Integrations.VisitReminderBackgroundTask>());
    }
}

/// <summary>
/// Texts customers about their visits through the site's SMS provider.
/// </summary>
[RequireFeatures("OrchardCore.Sms")]
public sealed class SmsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IVisitNoticeSender, Integrations.SmsVisitNoticeSender>();
    }
}

/// <summary>
/// Writes to customers about their visits through the site's email provider.
/// </summary>
[RequireFeatures("OrchardCore.Email")]
public sealed class EmailStartup : StartupBase
{
    /// <inheritdoc/>
    public override int Order => 10;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IVisitNoticeSender, Integrations.EmailVisitNoticeSender>();
    }
}

/// <summary>
/// Books Fleet's schedulable equipment for visits that need it.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Assets.Fleet")]
public sealed class FleetStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IDispatchEquipmentService, Integrations.FleetDispatchEquipmentService>();
    }
}

/// <summary>
/// Sends visit changes to open dispatch boards over SignalR.
/// </summary>
[RequireFeatures("OrchardCore.SignalR")]
public sealed class RealTimeStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<DispatchBoardNotifier>();
        services.AddScoped<IVisitObserver>(sp => sp.GetRequiredService<DispatchBoardNotifier>());
    }

    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapHub<DispatchHub>(SignalRHubRoutes.GetHubPath<DispatchHub>());
    }
}

/// <summary>
/// Registers the visit workflow events.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IVisitObserver, WorkflowVisitObserver>();
        services.AddActivity<VisitAssignedEvent, VisitAssignedEventDisplayDriver>();
        services.AddActivity<VisitStatusChangedEvent, VisitStatusChangedEventDisplayDriver>();
        services.AddActivity<VisitAtRiskEvent, VisitAtRiskEventDisplayDriver>();
    }
}
