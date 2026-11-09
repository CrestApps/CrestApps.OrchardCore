using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Geo.AzureMaps.Recipes;

/// <summary>
/// Describes the <c>AzureMapsSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AzureMapsSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(AzureMapsSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Azure Maps provider's settings. Choose the provider in GeoSettings.")
            .Properties(
                (nameof(AzureMapsSettings.Key), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description("The API key, stored encrypted with this tenant's data protection keys. A plain key written by the Settings step can't be read back, so enter the key in the admin under Settings > Maps and Geocoding instead.")))
            .AdditionalProperties(true);
}
