using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for AI data source settings.
/// </summary>
public sealed class AIDataSourceSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AIDataSourceSettings";

    /// <summary>
    /// Builds the schema for AI data source settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Default configuration for AI data source retrieval behavior.")
            .Properties(
                ("DefaultStrictness", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The default strictness level for data source queries (higher values are more strict).")),
                ("DefaultTopNDocuments", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The default number of top documents to retrieve from data sources.")))
            .AdditionalProperties(false);
}
