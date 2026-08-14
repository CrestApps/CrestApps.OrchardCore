using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for search settings.
/// </summary>
public sealed class SearchSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SearchSettings";

    /// <summary>
    /// Builds the schema for search settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the site search feature.")
            .Properties(
                ("DefaultIndexProfileName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the default search index profile.")),
                ("Placeholder", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The placeholder text for the search input.")),
                ("PageTitle", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The title displayed on the search results page.")))
            .AdditionalProperties(false);
}
