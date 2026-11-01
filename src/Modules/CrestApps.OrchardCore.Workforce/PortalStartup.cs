using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers the My Work portal: its pages, notifications, web push and the worker's calendar feed.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
public sealed class PortalStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforcePortalCore();
        services.AddDataMigration<PortalMigrations>()
            .AddIndexProvider<WorkforceInboxIndexProvider>()
            .AddIndexProvider<WebPushSubscriptionIndexProvider>()
            .AddIndexProvider<WorkerFeedIndexProvider>();
        services.AddScoped<MyWorkPortal>();
        services.AddPermissionProvider<PortalPermissionsProvider>();
        services.AddNavigationProvider<PortalAdminMenu>();
    }
}

/// <summary>
/// Sends the portal's notifications by email when the Email feature is on.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
[RequireFeatures("OrchardCore.Email")]
public sealed class PortalEmailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkforceNotificationChannel, EmailNotificationChannel>());
    }
}
