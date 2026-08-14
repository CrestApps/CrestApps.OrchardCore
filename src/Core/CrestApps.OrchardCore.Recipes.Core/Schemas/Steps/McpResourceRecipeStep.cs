using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the "McpResource" recipe step — creates or updates MCP resource records.
/// </summary>
public sealed class McpResourceRecipeStep : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => "McpResource";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchema CreateSchema()
    {
        var resourceDefinitionSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", RecipeStepSchemaBuilders.String().Description("Logical resource name exposed to MCP clients.")),
                ("Title", RecipeStepSchemaBuilders.String().Description("Optional display title. Imports usually derive this from DisplayText, so this field can be omitted.")),
                ("Uri", RecipeStepSchemaBuilders.String().Description("Resource URI or relative path. Imports accept full URIs such as ftp://{itemId}/path and may also accept the path portion that will be expanded to the final MCP URI.")),
                ("Description", RecipeStepSchemaBuilders.String().Description("Optional description shown to MCP clients.")),
                ("MimeType", RecipeStepSchemaBuilders.String().Description("Optional MIME type advertised to MCP clients.")))
            .Required("Name", "Uri")
            .AdditionalProperties(true);

        var propertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Source-specific resource metadata grouped by metadata object name. Known built-in metadata objects are FtpConnectionMetadata and SftpConnectionMetadata, and extra objects are allowed for future resource sources.")
            .Properties(
                ("FtpConnectionMetadata", KnownRecipeMetadataSchemaBuilders.BuildFtpConnectionMetadataSchema()),
                ("SftpConnectionMetadata", KnownRecipeMetadataSchemaBuilders.BuildSftpConnectionMetadataSchema()))
            .AdditionalProperties(true);

        var resourceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ItemId", RecipeStepSchemaBuilders.String().Description("Optional unique identifier. When supplied and found, the existing MCP resource is updated.")),
                ("DisplayText", RecipeStepSchemaBuilders.String().Description("Human-readable title of the MCP resource.")),
                ("Source", RecipeStepSchemaBuilders.String().Description("Resource source type. Known built-in values include 'file', 'media', 'recipe', 'recipe-schema', 'recipe-step-schema', 'content-item', 'content-type', 'ftp', and 'sftp'.")),
                ("CreatedUtc", RecipeStepSchemaBuilders.String().Description("Optional creation timestamp to preserve during import.")),
                ("ModifiedUtc", RecipeStepSchemaBuilders.String().Description("Optional last-modified timestamp to preserve during import.")),
                ("OwnerId", RecipeStepSchemaBuilders.String().Description("Optional owner user identifier.")),
                ("Author", RecipeStepSchemaBuilders.String().Description("Optional author name recorded with the resource.")),
                ("Resource", resourceDefinitionSchema.Description("The MCP resource definition exposed to clients.")),
                ("Properties", propertiesSchema))
            .Required("DisplayText", "Source", "Resource")
            .AdditionalProperties(true);

        return RecipeStepSchemaBuilders.BuildNamedStep(
            "McpResource",
            [("Resources", RecipeStepSchemaBuilders.Array(resourceSchema, 1).Description("The MCP resources to create or update."))],
            ["Resources"]);
    }
}
