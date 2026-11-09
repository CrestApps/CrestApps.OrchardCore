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
                ("IndexProfileName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name of the index profile used for document retrieval.")),
                ("TopN", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of document chunks to retrieve per query.")),
                ("RetrievalMode", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Chunk", "Hierarchical").Description("The document retrieval mode.")),
                ("AllowDocumentUploads", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether users can upload document files in chat interactions.")),
                ("AllowImageUploads", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether users can upload image files in chat interactions.")),
                ("MaxIndexableCharacters", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(50000).Description("The most characters a document may hold; a longer document is refused at upload rather than stored unsearchable. AI profiles may override it.")),
                ("DescribeFiguresInUploads", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether figures in uploaded documents are described by the Vision deployment while they are indexed. AI profiles may override it.")))
            .AdditionalProperties(false);
}
