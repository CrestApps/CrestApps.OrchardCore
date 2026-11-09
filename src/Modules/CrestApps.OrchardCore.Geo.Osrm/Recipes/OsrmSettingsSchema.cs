using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Geo.Osrm.Recipes;

/// <summary>
/// Describes the <c>OsrmSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class OsrmSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(OsrmSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The settings of the OSRM routing server used for travel times and routes. Choose the provider in GeoSettings.")
            .Properties(
                (nameof(OsrmSettings.BaseUrl), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The server's address, such as http://localhost:5000.")),
                (nameof(OsrmSettings.Profile), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("driving").Description("The routing profile the server was built with. Defaults to driving.")))
            .AdditionalProperties(true);
}
