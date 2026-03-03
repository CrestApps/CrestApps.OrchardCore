using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Reusable JSON schema fragments for common content structures.
/// </summary>
public static class ContentCommonSchemas
{
    /// <summary>
    /// A reusable schema for a generic content item object.
    /// Includes common content item metadata and allows additional properties.
    /// </summary>
    public static JsonSchemaBuilder ContentItemSchema
        => CreateContentItemSchema();

    /// <summary>
    /// Creates a schema for a generic content item object.
    /// </summary>
    /// <param name="contentTypes">Optional list of available content type names to enumerate.</param>
    public static JsonSchemaBuilder CreateContentItemSchema(IEnumerable<string> contentTypes = null)
    {
        var contentTypeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The type of content item.");

        if (contentTypes is not null)
        {
            var typeList = contentTypes.Where(type => !string.IsNullOrWhiteSpace(type)).Distinct().ToArray();

            if (typeList.Length > 0)
            {
                contentTypeSchema.Enum(typeList);
            }
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentItemId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The unique identifier for the content item.")),
                ("ContentItemVersionId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The version identifier for the content item.")),
                ("ContentType", contentTypeSchema),
                ("DisplayText", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The display text for the content item.")),
                ("Latest", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this is the latest version.")),
                ("Published", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether this content item is published.")),
                ("ModifiedUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The UTC date/time when the content item was last modified.")),
                ("PublishedUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The UTC date/time when the content item was published.")),
                ("CreatedUtc", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The UTC date/time when the content item was created.")),
                ("Owner", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The owner of the content item.")),
                ("Author", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The author of the content item.")))
            .Required("ContentItemId", "ContentType")
            .AdditionalProperties(true);
    }
}
