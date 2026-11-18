using System.Text.Json;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Reports the background geocoding queue and the chosen providers, as the Maps and Geocoding settings page shows them.
/// Provider keys are never returned; the last error is shown with anything that looks like a key hidden.
/// </summary>
internal sealed class GetGeocodingStatusTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getGeocodingStatus";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Reports the background geocoding queue: how many addresses are waiting, done and failed, the last error, and which providers are selected. Use it when addresses have no coordinates.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageGeoSettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var queue = await TerritoryCommands.GetQueueStatusAsync(services.GetRequiredService<ISession>());
        var settings = await services.GetRequiredService<IGeoSettingsService>().GetAsync();
        var geocoders = services.GetServices<IGeocodingProvider>()
            .DistinctBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new { name = provider.Name, displayName = provider.DisplayName })
            .ToList();
        var travel = services.GetServices<ITravelTimeProvider>()
            .Where(provider => provider.Name != GeoConstants.HaversineProviderName)
            .DistinctBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new { name = provider.Name, displayName = provider.DisplayName })
            .ToList();
        var routes = services.GetServices<IRouteProvider>()
            .Where(provider => provider.Name != GeoConstants.HaversineProviderName)
            .DistinctBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new { name = provider.Name, displayName = provider.DisplayName })
            .ToList();

        var geocoderInstalled = settings.GeocodingProvider is not null
            && geocoders.Any(provider => string.Equals(provider.name, settings.GeocodingProvider, StringComparison.OrdinalIgnoreCase));

        return Json(new
        {
            waiting = queue.Waiting,
            done = queue.Done,
            failed = queue.Failed,
            lastError = HideKeys(queue.LastError),
            geocodingProvider = settings.GeocodingProvider,
            travelTimeProvider = settings.TravelTimeProvider ?? GeoConstants.HaversineProviderName,
            routeProvider = settings.RouteProvider ?? GeoConstants.HaversineProviderName,
            availableGeocodingProviders = geocoders,
            availableTravelTimeProviders = travel,
            availableRouteProviders = routes,
            message = settings.GeocodingProvider is null
                ? "No geocoding provider is chosen, so addresses wait without coordinates. Choose one under Settings, Maps and Geocoding (enable Azure Maps or Google Maps first, and enter its key there); waiting addresses are then tried at the next run."
                : !geocoderInstalled
                    ? $"The chosen geocoding provider '{settings.GeocodingProvider}' is not enabled, so addresses wait. Enable its feature or choose another provider."
                    : queue.Failed > 0
                        ? "Some addresses were given up after their attempts failed or found no match; see lastError. The Geocode addresses background task tries waiting addresses every minute."
                        : "The Geocode addresses background task geocodes waiting addresses every minute.",
        });
    }
}
