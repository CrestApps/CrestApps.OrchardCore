namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Reusable JSON schema fragments for common content structures.
/// </summary>
public static class ContentCommonSchemas
{
    /// <summary>
    /// A reusable schema for a generic content item object.
    /// Includes ContentType, ContentItemId, DisplayText and allows additional properties.
    /// </summary>
    public static JsonSchemaBuilder ContentItemSchema
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentItemId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The unique identifier for the content item.")),
                ("ContentType", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The content type of the content item.")),
                ("DisplayText", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The display text of the content item.")),
                ("Latest", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this is the latest version.")),
                ("Published", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this content item is published.")))
            .Required("ContentType")
            .AdditionalProperties(true);
}
