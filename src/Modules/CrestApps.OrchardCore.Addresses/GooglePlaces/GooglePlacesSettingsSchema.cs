using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.Addresses.GooglePlaces;

/// <summary>
/// Describes the <c>GooglePlacesSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class GooglePlacesSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(GooglePlacesSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Google Places address suggestions settings.")
            .Properties(
                (nameof(GooglePlacesSettings.ApiKey), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String, SchemaValueType.Null)
                    .Description("The Google Places API key, encrypted with the site's data protection keys. A clear-text key set by a recipe can't be read, so enter the key under Settings in the admin instead; a value exported from another site only works where the same data protection keys are used.")))
            .AdditionalProperties(true);
}
