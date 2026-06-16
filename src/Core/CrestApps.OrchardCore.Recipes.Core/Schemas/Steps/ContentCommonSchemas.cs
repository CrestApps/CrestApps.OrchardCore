using Json.Schema;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

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
        var typeList = NormalizeContentTypes(contentTypes);

        return CreateContentItemSchema(typeList, [], null, null);
    }

    /// <summary>
    /// Creates a schema for a content item object using Orchard Core content type definitions.
    /// </summary>
    /// <param name="contentTypeDefinitions">The content type definitions that describe the known parts and fields.</param>
    public static JsonSchemaBuilder CreateContentItemSchema(IEnumerable<ContentTypeDefinition> contentTypeDefinitions)
    {
        var definitions = NormalizeContentTypeDefinitions(contentTypeDefinitions);
        var contentTypes = definitions.Select(definition => definition.Name).ToArray();

        return CreateContentItemSchema(contentTypes, definitions, null, null);
    }

    /// <summary>
    /// Creates a schema for a content item object using Orchard Core content type definitions
    /// and field-specific schema fragments.
    /// </summary>
    /// <param name="contentTypeDefinitions">The content type definitions that describe the known parts and fields.</param>
    /// <param name="partSchemas">The available content part schemas keyed by Orchard part name.</param>
    /// <param name="fieldSchemas">The available field schemas keyed by Orchard field type name.</param>
    public static JsonSchemaBuilder CreateContentItemSchema(
        IEnumerable<ContentTypeDefinition> contentTypeDefinitions,
        IReadOnlyDictionary<string, JsonSchemaBuilder> partSchemas,
        IReadOnlyDictionary<string, JsonSchemaBuilder> fieldSchemas)
    {
        var definitions = NormalizeContentTypeDefinitions(contentTypeDefinitions);
        var contentTypes = definitions.Select(definition => definition.Name).ToArray();

        return CreateContentItemSchema(contentTypes, definitions, partSchemas, fieldSchemas);
    }

    private static JsonSchemaBuilder CreateContentItemSchema(
        IReadOnlyList<string> contentTypes,
        ContentTypeDefinition[] contentTypeDefinitions,
        IReadOnlyDictionary<string, JsonSchemaBuilder> partSchemas,
        IReadOnlyDictionary<string, JsonSchemaBuilder> fieldSchemas)
    {
        var contentTypeSchema = CreateContentTypeValueSchema(contentTypes);
        var schema = new JsonSchemaBuilder()
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
                    .Description("The author of the content item."))
            )
            .Required("ContentType")
            .AdditionalProperties(true);

        if (contentTypeDefinitions.Length == 0)
        {
            return schema;
        }

        return schema.AllOf(contentTypeDefinitions.Select(definition => CreateContentTypeSchema(definition, partSchemas, fieldSchemas)).ToArray());
    }

    private static JsonSchemaBuilder CreateContentTypeSchema(
        ContentTypeDefinition definition,
        IReadOnlyDictionary<string, JsonSchemaBuilder> partSchemas,
        IReadOnlyDictionary<string, JsonSchemaBuilder> fieldSchemas)
    {
        var partProperties = definition.Parts?
            .Where(part => !string.IsNullOrWhiteSpace(part.Name))
            .GroupBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(part => part.Name, StringComparer.Ordinal)
            .Select(part => CreatePartProperty(part, partSchemas, fieldSchemas))
            .ToArray() ?? [];

        if (partProperties.Length == 0)
        {
            return new JsonSchemaBuilder();
        }

        return new JsonSchemaBuilder()
            .If(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const(definition.Name)))
                .Required("ContentType"))
            .Then(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(partProperties)
                .AdditionalProperties(true));
    }

    private static (string Name, JsonSchemaBuilder Schema) CreatePartProperty(
        ContentTypePartDefinition definition,
        IReadOnlyDictionary<string, JsonSchemaBuilder> partSchemas,
        IReadOnlyDictionary<string, JsonSchemaBuilder> fieldSchemas)
    {
        var fieldProperties = definition.PartDefinition?.Fields?
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => CreateFieldProperty(field, fieldSchemas))
            .ToArray();

        var description = $"The '{definition.Name}' part available on '{definition.ContentTypeDefinition?.Name ?? "the selected content type"}'.";
        var schema = partSchemas is not null &&
            partSchemas.TryGetValue(definition.Name, out var partSchema)
            ? partSchema
            : new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true);

        if (fieldProperties is null || fieldProperties.Length == 0)
        {
            return (definition.Name, new JsonSchemaBuilder()
                .Description(description)
                .AllOf(schema));
        }

        return (definition.Name, new JsonSchemaBuilder()
            .Description(description)
            .AllOf(
                schema,
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(fieldProperties)
                    .AdditionalProperties(true)));
    }

    private static (string Name, JsonSchemaBuilder Schema) CreateFieldProperty(
        ContentPartFieldDefinition definition,
        IReadOnlyDictionary<string, JsonSchemaBuilder> fieldSchemas)
    {
        if (!string.IsNullOrWhiteSpace(definition.FieldDefinition?.Name) &&
            fieldSchemas is not null &&
            fieldSchemas.TryGetValue(definition.FieldDefinition.Name, out var fieldSchema))
        {
            return (definition.Name, fieldSchema);
        }

        return (definition.Name, new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true));
    }

    private static JsonSchemaBuilder CreateContentTypeValueSchema(IReadOnlyList<string> contentTypes)
    {
        var schema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("The type of content item.");

        if (contentTypes.Count > 0)
        {
            schema = schema.Enum(contentTypes);
        }

        return schema;
    }

    private static string[] NormalizeContentTypes(IEnumerable<string> contentTypes)
        => contentTypes?
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray() ?? [];

    private static ContentTypeDefinition[] NormalizeContentTypeDefinitions(IEnumerable<ContentTypeDefinition> contentTypeDefinitions)
        => contentTypeDefinitions?
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray() ?? [];
}
