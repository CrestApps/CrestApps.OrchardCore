using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Geo.Osrm.Recipes;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// Registers OSRM as a travel-time and route provider.
/// </summary>
public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The tenant's configuration.</param>
    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<OsrmNetworkOptions>(_shellConfiguration.GetSection(OsrmNetworkOptions.SectionName));

        // The server's address is an administrator's, so the client never reaches the site's own network unless the host
        // opts in for a self-hosted server (and never the link-local metadata service), and follows no redirect.
        services.AddEgressGuardedHttpClient(
            OsrmProvider.HttpClientName,
            client => client.Timeout = TimeSpan.FromSeconds(30),
            configureResilience: null,
            allowPrivateNetwork: serviceProvider => serviceProvider.GetService<IOptions<OsrmNetworkOptions>>()?.Value.AllowPrivateNetwork == true);

        services.TryAddScoped<OsrmProvider>();
        services.AddScoped<ITravelTimeProvider>(sp => sp.GetRequiredService<OsrmProvider>());
        services.AddScoped<IRouteProvider>(sp => sp.GetRequiredService<OsrmProvider>());
        services.AddSiteDisplayDriver<OsrmSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GeoConstants.SettingsGroupId, GeoPermissions.ManageGeoSettings);
    }
}

/// <summary>
/// Describes the Osrm site settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, OsrmSettingsSchema>();
    }
}
