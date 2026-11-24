using CrestApps.Core.AI;
using CrestApps.OrchardCore.Geo.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// Registers the service territory AI tools whenever the Geo and AI features are on. They need
/// <c>ManageGeoTerritories</c>, as the Territories page does; finding the territory of a street address geocodes it,
/// which also needs <c>ManageGeoSettings</c>. Deleting a territory asks for approval.
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
            .WithDescription(S["Finds which service territory covers a street address, a latitude/longitude or a postal code. A street address is geocoded first with the configured maps provider (a billable call that needs the Manage Geo settings permission), and the result returns the point it found."])
            .WithCategory(S["Geo"])
            .RequireApproval()
            .Selectable();
    }
}
