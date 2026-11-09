using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using static CrestApps.OrchardCore.Geo.Recipes.GeoSchemaBuilder;

namespace CrestApps.OrchardCore.Geo.Recipes;

/// <summary>
/// Describes the Geo recipe step to CrestApps Recipes.
/// </summary>
internal sealed class GeoRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => GeoRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var member = Object(
            "A team or person that serves the territory.",
            ("Kind", new JsonSchemaBuilder().Type(SchemaValueType.String).Examples("Team", "Worker").Description("The kind of member, by the module that owns it, such as Team or Worker.")),
            ("Id", Text("The member's identifier in the module that owns it.")),
            ("Role", Enum<TerritoryRole>("Whether the territory is theirs first or as a backup.")));

        var territory = Object(
            "A service territory: an area served together, drawn as a shape, listed as postal codes, or both.",
            ("ItemId", ItemId("territory")),
            ("Name", Text("The territory name.")),
            ("ParentId", Text("The ItemId of the territory it is part of.")),
            ("Shape", Text("The shape, as GeoJSON text: a Polygon or MultiPolygon geometry, Feature or FeatureCollection.")),
            ("PostalCodes", Strings("The postal codes; one ending with * matches every code that starts with the rest.")),
            ("TimeZoneId", Text("The IANA time zone; null for the site's.")),
            ("TravelSpeedKmh", OptionalNumber("The average travel speed in km/h; null for the site's.")),
            ("Members", Array(member, "The people and teams that serve it.")));

        _cached ??= Step(
            GeoRecipeStep.RecipeName,
            "Creates or updates service territories. Territories are matched by ItemId only; an existing territory takes only the members given. List parent territories before their children.",
            ("Territories", Array(territory, "The service territories, parents first.")));

        return ValueTask.FromResult(_cached);
    }
}
