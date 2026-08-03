using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.Configuration;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.BackgroundTasks;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Drivers;
using CrestApps.OrchardCore.Telephony.Filters;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Migrations;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.FileStorage.FileSystem;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

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
        services.ValidateTenantOptionsOnActivation();

        services
            .AddOptions<TelephonyCommandOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps_Telephony:Commands"))
            .Validate(
                options => options.Timeout >= TimeSpan.FromSeconds(TelephonyCommandOptions.MinimumTimeoutSeconds) &&
                    options.Timeout <= TimeSpan.FromSeconds(TelephonyCommandOptions.MaximumTimeoutSeconds),
                "The Telephony command timeout must be between one second and two minutes.")
            .ValidateOnStart();

        services
            .AddOptions<TelephonyCoordinationOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps_Telephony:Coordination"))
            .Validate(
                options => options.InteractionLockTimeout > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:InteractionLockTimeout' must be greater than zero.")
            .Validate(
                options => options.InteractionLockExpiration > options.InteractionLockTimeout,
                "'CrestApps_Telephony:Coordination:InteractionLockExpiration' must exceed 'InteractionLockTimeout', otherwise the reconciliation lease expires while a peer is still waiting for it and two sweeps run at once.")
            .Validate(
                options => options.NewInteractionGracePeriod > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:NewInteractionGracePeriod' must be greater than zero, otherwise reconciliation can terminate an interaction another node has only just written.")
            .Validate(
                options => options.TokenRefreshLockTimeout > TimeSpan.Zero,
                "'CrestApps_Telephony:Coordination:TokenRefreshLockTimeout' must be greater than zero.")
            .Validate(
                options => options.TokenRefreshLockExpiration > options.TokenRefreshLockTimeout,
                "'CrestApps_Telephony:Coordination:TokenRefreshLockExpiration' must exceed 'TokenRefreshLockTimeout', otherwise the refresh lease expires while a peer is still waiting for it and two refreshes run at once.")
            .ValidateOnStart();

        services.TryAddSingleton<IProviderIdentityResolver, ProviderIdentityResolver>();
        services.AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet));
        services.AddScoped<IVoiceIngressGate, VoiceIngressGate>();
        services.AddScoped<INormalizedVoiceEventIngestor, NormalizedVoiceEventIngestor>();
        services.AddScoped<INormalizedVoiceEventHandler, TelephonyCallHistoryVoiceEventHandler>();
        services.AddScoped<ITelephonyProviderResolver, DefaultTelephonyProviderResolver>();
        services.AddScoped<IOutboundCallScreeningService, DefaultOutboundCallScreeningService>();
        services.AddScoped<ITelephonyService, DefaultTelephonyService>();
        services.AddScoped<ITelephonyCommandExecutor, DefaultTelephonyCommandExecutor>();
        services.AddScoped<IIncomingCallDispatcher, DefaultIncomingCallDispatcher>();
        services.AddTransient<IPostConfigureOptions<TelephonySettings>, TelephonySettingsConfiguration>();

        services.AddScoped<ITelephonyUserAccessor, DefaultTelephonyUserAccessor>();
        services.AddScoped<ITelephonyUserTokenStore, DefaultTelephonyUserTokenStore>();
        services.AddScoped<ITelephonyAuthenticationService, DefaultTelephonyAuthenticationService>();

        services.AddScoped<ITelephonyInteractionStore, DefaultTelephonyInteractionStore>();
        services.AddScoped<ITelephonyInteractionSynchronizationService, TelephonyInteractionSynchronizationService>();
        services.AddSingleton<IBackgroundTask, TelephonyInteractionReconciliationBackgroundTask>();
        services.AddIndexProvider<TelephonyInteractionIndexProvider>();
        services.AddDataMigration<TelephonyInteractionMigrations>();

        // The default recording media store keeps encrypted recordings under a tenant-scoped application-data
        // folder, so recordings ingested by any voice provider are namespaced per tenant and never observable
        // across tenants. The abstraction is pluggable, so a deployment can replace this with a cloud-backed
        // store without touching ingest callers.
        services.AddSingleton<IRecordingMediaStore>(serviceProvider =>
        {
            var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
            var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
            var logger = serviceProvider.GetRequiredService<ILogger<FileSystemStore>>();
            var dataProtectionProvider = serviceProvider.GetRequiredService<IDataProtectionProvider>();
            var path = Path.Combine(
                shellOptions.ShellsApplicationDataPath,
                shellOptions.ShellsContainerName,
                shellSettings.Name,
                TelephonyConstants.RecordingMediaFolderName);
            var fileStore = new FileSystemStore(path, logger);

            return new LocalEncryptedRecordingMediaStore(fileStore, dataProtectionProvider);
        });
        services.AddScoped<IModularTenantEvents, RecordingMediaTenantEvents>();

        services
            .AddPermissionProvider<TelephonyPermissionProvider>()
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
/// Registers the telephony provider settings screen.
/// </summary>
/// <remarks>
/// The telephony services, hub and provider bindings are usable without any screen, so a headless deployment can
/// enable the capability and configure it from a recipe or an API without carrying an administration surface.
/// </remarks>
[Feature(TelephonyConstants.Feature.Admin)]
public sealed class TelephonyAdminStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSiteDisplayDriver<TelephonySettingsDisplayDriver>()
            .AddNavigationProvider<TelephonyAdminMenu>();
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
