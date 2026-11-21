using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Migrations;
using CrestApps.OrchardCore.Workforce.Navigation;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Registers the My Work portal: its pages, notifications, web push and the worker's calendar feed.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
public sealed class PortalStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalStartup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration.</param>
    public PortalStartup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkforcePortalCore();
        services.Configure<WebPushOptions>(_shellConfiguration.GetSection(WebPushOptions.SectionName));
        services.AddStaffHomeLinkProvider<WorkforceStaffHomeLinks>();
        services.AddSingleton<IBackgroundTask, BackgroundTasks.ShiftReminderBackgroundTask>();
        services.AddDataMigration<PortalMigrations>()
            .AddIndexProvider<WorkforceInboxIndexProvider>()
            .AddIndexProvider<WebPushSubscriptionIndexProvider>()
            .AddIndexProvider<WorkerFeedIndexProvider>()
            .AddIndexProvider<ShiftReminderLogIndexProvider>();
        services.AddScoped<MyWorkPortal>();
        services.Configure<TimeClockTimeTrackerOptions>(options =>
        {
            options.ClockUrl = "~/Admin/my-work";
            options.ControlsViewComponent = Portal.TimeClockControlsViewComponent.Name;
        });
        services.AddPermissionProvider<PortalPermissionsProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, Portal.WorkerAdminAccessAuthorizationHandler>());
        services.TryAddScoped(provider => new Lazy<IAuthorizationService>(provider.GetRequiredService<IAuthorizationService>));
        services.AddNavigationProvider<PortalAdminMenu>();

        // A device that kept My Work pages offline forgets them when its user signs out.
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILogoutFormEvent, MyWorkSignOut>());
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapMyWorkSignedOut();
    }
}

/// <summary>
/// Sends the portal's notifications by email when the Email feature is on.
/// </summary>
[Feature(WorkforceConstants.Features.Portal)]
[RequireFeatures(OrchardCoreFeatureIds.Email)]
public sealed class PortalEmailStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IWorkforceNotificationChannel, EmailNotificationChannel>());
    }
}
