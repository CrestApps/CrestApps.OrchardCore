using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for interaction document settings.
/// </summary>
public sealed class InteractionDocumentSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "InteractionDocumentSettings";

    /// <summary>
    /// Builds the schema for interaction document settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for AI document retrieval during chat interactions.")
            .Properties(
                ("IndexProfileName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the index profile used for document retrieval.")),
                ("TopN", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of document chunks to retrieve per query.")),
                ("RetrievalMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chunk", "Hierarchical").Description("The document retrieval mode.")))
            .AdditionalProperties(false);
}
