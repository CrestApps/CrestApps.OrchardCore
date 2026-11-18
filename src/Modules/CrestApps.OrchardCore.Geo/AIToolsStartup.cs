using CrestApps.Core.AI;
using CrestApps.OrchardCore.Geo.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// Registers the service territory and geocoding AI tools whenever the Geo and AI features are on. The territory tools
/// need <c>ManageGeoTerritories</c> and the geocoding tools <c>ManageGeoSettings</c>, as their admin pages do. Deleting a
/// territory asks for approval, and so does a geocoding lookup, which the site pays its provider for.
/// </summary>
[RequireFeatures(AIFeatureIds.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListServiceTerritoriesTool>(ListServiceTerritoriesTool.TheName)
            .WithTitle(S["List Service Territories"])
            .WithDescription(S["Lists the service territories with parent, postal codes, time zone, travel speed, members and whether a shape is drawn; pass an id to also get its GeoJSON shape and the member kinds that can be assigned."])
            .WithCategory(S["Geo"])
            .Selectable();

        services.AddCoreAITool<SaveServiceTerritoryTool>(SaveServiceTerritoryTool.TheName)
            .WithTitle(S["Save Service Territory"])
            .WithDescription(S["Creates or updates a service territory: name, parent, GeoJSON shape or postal codes, time zone, travel speed and members by kind. Members of kinds you do not pass are kept."])
            .WithCategory(S["Geo"])
            .Selectable();

        services.AddCoreAITool<DeleteServiceTerritoryTool>(DeleteServiceTerritoryTool.TheName)
            .WithTitle(S["Delete Service Territory"])
            .WithDescription(S["Deletes a service territory. Its child territories move up to its parent."])
            .WithCategory(S["Geo"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<FindServiceTerritoryTool>(FindServiceTerritoryTool.TheName)
            .WithTitle(S["Find Service Territory"])
            .WithDescription(S["Finds which service territory covers a latitude/longitude or a postal code. Use geocodeAddress first when you only have a street address."])
            .WithCategory(S["Geo"])
            .Selectable();

        services.AddCoreAITool<GeocodeAddressTool>(GeocodeAddressTool.TheName)
            .WithTitle(S["Geocode Address"])
            .WithDescription(S["Looks up the coordinates of an address, or the address of coordinates, with the configured maps provider. It is a billable provider call and fails when no geocoding provider is set."])
            .WithCategory(S["Geo"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<GetGeocodingStatusTool>(GetGeocodingStatusTool.TheName)
            .WithTitle(S["Get Geocoding Status"])
            .WithDescription(S["Reports the background geocoding queue: how many addresses are waiting, done and failed, the last error, and which providers are selected. Use it when addresses have no coordinates."])
            .WithCategory(S["Geo"])
            .Selectable();
    }
}
