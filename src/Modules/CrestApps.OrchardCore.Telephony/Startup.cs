using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.OrchardCore.Telephony.Drivers;
using CrestApps.OrchardCore.Telephony.Filters;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Navigation;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Registers the provider-agnostic telephony services, settings, and SignalR hub.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ITelephonyProviderResolver, DefaultTelephonyProviderResolver>();
        services.AddScoped<ITelephonyService, DefaultTelephonyService>();
        services.AddScoped<IIncomingCallDispatcher, DefaultIncomingCallDispatcher>();
        services.AddTransient<IPostConfigureOptions<TelephonySettings>, TelephonySettingsConfiguration>();

        services.AddScoped<ITelephonyUserAccessor, DefaultTelephonyUserAccessor>();
        services.AddScoped<ITelephonyUserTokenStore, DefaultTelephonyUserTokenStore>();
        services.AddScoped<ITelephonyAuthenticationService, DefaultTelephonyAuthenticationService>();

        services.AddScoped<ITelephonyInteractionStore, DefaultTelephonyInteractionStore>();
        services.AddScoped<ITelephonyInteractionSynchronizationService, TelephonyInteractionSynchronizationService>();
        services.AddScoped<IModularTenantEvents, TelephonyInteractionTenantEvents>();
        services.AddSingleton<IBackgroundTask, TelephonyInteractionReconciliationBackgroundTask>();
        services.AddIndexProvider<TelephonyInteractionIndexProvider>();
        services.AddDataMigration<TelephonyInteractionMigrations>();

        services
            .AddPermissionProvider<TelephonyPermissionProvider>()
            .AddSiteDisplayDriver<TelephonySettingsDisplayDriver>()
            .AddNavigationProvider<TelephonyAdminMenu>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        HubRouteManager.MapHub<TelephonyHub>(routes);

        routes.MapAreaControllerRoute(
            name: TelephonyConstants.RouteNames.OAuthConnect,
            areaName: TelephonyConstants.Feature.Area,
            pattern: "Telephony/Connect",
            defaults: new { controller = "TelephonyOAuth", action = "Connect" });

        routes.MapAreaControllerRoute(
            name: TelephonyConstants.RouteNames.OAuthCallback,
            areaName: TelephonyConstants.Feature.Area,
            pattern: "Telephony/Connect/Callback",
            defaults: new { controller = "TelephonyOAuth", action = "Callback" });

        routes.MapAreaControllerRoute(
            name: TelephonyConstants.RouteNames.OAuthDisconnect,
            areaName: TelephonyConstants.Feature.Area,
            pattern: "Telephony/Disconnect",
            defaults: new { controller = "TelephonyOAuth", action = "Disconnect" });
    }
}

/// <summary>
/// Registers the soft phone feature.
/// </summary>
[Feature(TelephonyConstants.Feature.SoftPhone)]
public sealed class SoftPhoneWidgetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SoftPhoneWidgetSettingsDisplayDriver>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<SoftPhoneWidgetFilter>();
        });
    }
}
