using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for taxonomy contents admin list settings.
/// </summary>
public sealed class TaxonomyContentsAdminListSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "TaxonomyContentsAdminListSettings";

    /// <summary>
    /// Builds the schema for taxonomy contents admin list settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for taxonomy filters in the content admin list.")
            .Properties(
                ("TaxonomyContentItemIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The content item IDs of taxonomies to use as filters.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(false);
}
