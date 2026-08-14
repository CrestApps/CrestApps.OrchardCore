using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for layer settings.
/// </summary>
public sealed class LayerSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "LayerSettings";

    /// <summary>
    /// Builds the schema for layer settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the layers feature.")
            .Properties(
                ("Zones", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The list of available zones for widget placement.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(false);
}
