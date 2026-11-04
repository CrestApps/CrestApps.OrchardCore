using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.AzureMaps;

/// <summary>
/// Registers Azure Maps as a geocoding, travel-time and route provider.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient(AzureMapsProvider.HttpClientName, client =>
            {
                client.BaseAddress = new Uri("https://atlas.microsoft.com/");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        services.TryAddScoped<AzureMapsProvider>();
        services.AddScoped<IGeocodingProvider>(sp => sp.GetRequiredService<AzureMapsProvider>());
        services.AddScoped<ITravelTimeProvider>(sp => sp.GetRequiredService<AzureMapsProvider>());
        services.AddScoped<IRouteProvider>(sp => sp.GetRequiredService<AzureMapsProvider>());
        services.AddSiteDisplayDriver<AzureMapsSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(GeoConstants.SettingsGroupId, GeoPermissions.ManageGeoSettings);
    }
}
