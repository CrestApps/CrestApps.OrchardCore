using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.Core;
using CrestApps.Core.Diagnostics;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.Core.Telephony.Models;
using CrestApps.Core.Data.YesSql.Telephony;
using CrestApps.Core.Telephony.Services;
using CrestApps.OrchardCore.Telephony.Drivers;
using CrestApps.OrchardCore.Telephony.Endpoints;
using CrestApps.Core.Telephony.Endpoints;
using CrestApps.OrchardCore.Telephony.Filters;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.Core.Data.YesSql.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.Core.Data.YesSql.Telephony.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement;
using OrchardCore.Data.Migration;
using OrchardCore.Data;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using CrestApps.Core.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Registers the provider-agnostic telephony services, settings, and SignalR hub.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration used to bind Telephony options.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreHostSeams();
        services.AddTelephonyOperationAuthorization();

        services.ValidateTenantOptionsOnActivation();

        // The telephony core, and the parts of it this module turns on. The options come from one section
        // with the same child names the module has always bound.
        services.AddCoreTelephony(_shellConfiguration.GetSection("CrestApps_Telephony"));

        // The soft-phone pushes go through the framework notifier over this module's hub, which is what
        // carries the host's authorization. Everything that raises a push depends on the notifier instead,
        // so no service needs to name the hub.
        services.AddCoreTelephonySoftPhoneNotifier<TelephonyHub>();

        services.AddCoreTelephonyVoiceIngress();
        services.AddCoreTelephonyCalling();

        // The telephony settings are read as options by the framework services, and the settings screen asks
        // the options system to refresh when they are saved.
        services.AddSiteSettingsOptions<TelephonySettings>();
        services.AddSignalOptionsChangeTokenSource<TelephonyProviderOptions>();

        services.AddCoreTelephonyAuthentication();

        // Persistence for the two framework stores, and the index providers that keep their tables in step.
        services.AddCoreTelephonyStoresYesSql();

        services.AddCoreTelephonyInteractions();
        services.AddCoreTelephonyExtensions();

        // The Orchard side of the same features: the schema, the screens, and the index over Orchard's own
        // user documents, which only a host can describe.
        services.AddDataMigration<TelephonyExtensionIndexMigrations>();
        services.AddDisplayDriver<TelephonyExtension, TelephonyExtensionDisplayDriver>();
        services.AddNavigationProvider<TelephonyExtensionsAdminMenu>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, TelephonyInteractionReconciliationBackgroundTask>());
        services.AddIndexProvider<TelephonyUserConnectionIndexProvider>();
        services.AddDataMigration<TelephonyInteractionMigrations>();
        services.AddDataMigration<TelephonyUserConnectionIndexMigrations>();

        // Every telephony document written before the move records a type name that no longer resolves, so
        // this runs before anything tries to read one.
        services.AddDataMigration<TelephonyLegacyDocumentTypeNameMigrations>();

        // The default recording media store keeps encrypted recordings under a tenant-scoped application-data
        // folder, so recordings ingested by any voice provider are namespaced per tenant and never observable
        // across tenants. The abstraction is pluggable, so a deployment can replace this with a cloud-backed
        // store without touching ingest callers.
        services.AddCoreTelephonyLocalRecordingStore(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();

            return Path.Combine(
                shellOptions.ShellsApplicationDataPath,
                shellOptions.ShellsContainerName,
                shellSettings.Name,
                TelephonyConstants.RecordingMediaFolderName);
        });
        services.AddScoped<IModularTenantEvents, RecordingMediaTenantEvents>();

        services
            .AddPermissionProvider<TelephonyPermissionProvider>()
            .AddResourceConfiguration<ResourceManagementOptionsConfiguration>()
            .AddSiteDisplayDriver<TelephonySettingsDisplayDriver>()
            .AddNavigationProvider<TelephonyAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The OAuth connect/callback/disconnect endpoints are attribute-routed on TelephonyOAuthController
        // (their named routes are preserved), so only the SignalR hub is mapped here.
        routes.MapHub<TelephonyHub>(SignalRHubRoutes.GetHubPath<TelephonyHub>());
    }
}

/// <summary>
/// Registers the shared soft phone client. This feature is enabled by dependency only; the soft phone widget
/// and the browser-extension endpoint both depend on it and reuse the presenter and resources it provides.
/// </summary>
[Feature(TelephonyFeatures.SoftPhoneCore)]
public sealed class SoftPhoneCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISoftPhoneWidgetPresenter, SoftPhoneWidgetPresenter>();

        // Loads the phone-field dialer "call" button on demand, only where a phone field renders (see the provider).
        services.AddShapeTableProvider<PhoneFieldDialerShapeTableProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // The phone-field "call" button posts here to start a call on the caller's own soft phone, wherever it is
        // connected. It belongs to the soft phone core so it is available in both the widget and extension surfaces.
        routes.MapSoftPhoneDialerEndpoints();
    }
}

/// <summary>
/// Registers the soft phone widget feature: the admin auto-injected floating phone and the placeable
/// front-end Soft Phone widget.
/// </summary>
[Feature(TelephonyFeatures.SoftPhone)]
public sealed class SoftPhoneWidgetStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSiteDisplayDriver<SoftPhoneWidgetSettingsDisplayDriver>();

        services
            .AddContentPart<SoftPhonePart>()
            .UseDisplayDriver<SoftPhonePartDisplayDriver>();

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<SoftPhoneWidgetFilter>();
        });
    }
}

/// <summary>
/// Registers the soft phone browser-extension feature: the standalone <c>/softphone</c> page and its
/// configuration endpoint hosted by the CrestApps Soft Phone browser extension.
/// </summary>
[Feature(TelephonyFeatures.SoftPhoneExtension)]
public sealed class SoftPhoneExtensionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The browser-extension soft phone has no in-page widget filter, so signal soft-phone presence here too;
        // otherwise the phone-field dialer "call" button (gated on that flag) never renders when the extension is
        // the only soft phone surface.
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<SoftPhoneExtensionDialerFilter>();
        });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapSoftPhoneExtensionConfigurationEndpoint();
    }
}

[RequireFeatures("OrchardCore.Contents")]
public sealed class ContentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // The Soft Phone widget content type lets an operator place the floating phone on the front end
        // through Design > Widgets, instead of it being auto-injected there.
        services.AddDataMigration<SoftPhoneWidgetMigrations>();
    }
}
