using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Json.Schema;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Builds content item JSON schemas from Orchard Core content definitions and schema contributors.
/// </summary>
public sealed class ContentItemSchemaService : IContentItemSchemaService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IContentSchemaDefinition> _schemaDefinitions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemSchemaService"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="schemaDefinitions">The registered content schema contributors.</param>
    public ContentItemSchemaService(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentSchemaDefinition> schemaDefinitions)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _schemaDefinitions = schemaDefinitions;
    }

    /// <inheritdoc />
    public ValueTask<JsonSchemaBuilder> GetGenericSchemaAsync(IEnumerable<string> contentTypes = null, CancellationToken cancellationToken = default)
    {
        var typeList = NormalizeContentTypes(contentTypes);

        return ValueTask.FromResult(CreateContentItemSchema(typeList));
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var definitions = NormalizeContentTypeDefinitions(await _contentDefinitionManager.ListTypeDefinitionsAsync());

        return await BuildSchemaAsync(definitions, definitions, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetSchemaAsync(string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var knownDefinitions = NormalizeContentTypeDefinitions(await _contentDefinitionManager.ListTypeDefinitionsAsync());
        var definition = knownDefinitions.FirstOrDefault(x => string.Equals(x.Name, contentType, StringComparison.OrdinalIgnoreCase))
            ?? await _contentDefinitionManager.GetTypeDefinitionAsync(contentType);

        return definition is null
            ? null
            : await BuildSchemaAsync([definition], knownDefinitions, cancellationToken);
    }

    private async ValueTask<JsonSchemaBuilder> BuildSchemaAsync(
        ContentTypeDefinition[] selectedDefinitions,
        ContentTypeDefinition[] knownDefinitions,
        CancellationToken cancellationToken)
    {
        var partSchemaDefinitions = _schemaDefinitions
            .OfType<IContentPartSchemaDefinition>()
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .ToLookup(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
        var fieldSchemaDefinitions = _schemaDefinitions
            .OfType<IContentFieldSchemaDefinition>()
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .ToLookup(definition => definition.Name, StringComparer.OrdinalIgnoreCase);

        return await CreateComposedContentItemSchemaAsync(
            selectedDefinitions,
            knownDefinitions,
            partSchemaDefinitions,
            fieldSchemaDefinitions,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    private async ValueTask<JsonSchemaBuilder> CreateComposedContentItemSchemaAsync(
        ContentTypeDefinition[] selectedDefinitions,
        ContentTypeDefinition[] knownDefinitions,
        ILookup<string, IContentPartSchemaDefinition> partSchemaDefinitions,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        HashSet<string> activeContentTypes,
        CancellationToken cancellationToken)
    {
        var contentTypes = selectedDefinitions.Select(definition => definition.Name).ToArray();
        var schema = CreateContentItemSchema(contentTypes);

        if (selectedDefinitions.Length == 0)
        {
            return schema;
        }

        var contentTypeSchemas = new List<JsonSchemaBuilder>(selectedDefinitions.Length);

        foreach (var definition in selectedDefinitions)
        {
            contentTypeSchemas.Add(await CreateContentTypeSchemaAsync(
                definition,
                knownDefinitions,
                partSchemaDefinitions,
                fieldSchemaDefinitions,
                activeContentTypes,
                cancellationToken));
        }

        return schema.AllOf(contentTypeSchemas.ToArray());
    }

    private async ValueTask<JsonSchemaBuilder> CreateContentTypeSchemaAsync(
        ContentTypeDefinition definition,
        ContentTypeDefinition[] knownDefinitions,
        ILookup<string, IContentPartSchemaDefinition> partSchemaDefinitions,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        HashSet<string> activeContentTypes,
        CancellationToken cancellationToken)
    {
        var nextActiveContentTypes = new HashSet<string>(activeContentTypes, StringComparer.OrdinalIgnoreCase)
        {
            definition.Name,
        };
        var parts = definition.Parts?
            .Where(part => !string.IsNullOrWhiteSpace(part.Name))
            .GroupBy(part => part.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(part => part.Name, StringComparer.Ordinal)
            .ToArray() ?? [];
        var partProperties = new List<(string Name, JsonSchemaBuilder Schema)>();

        foreach (var part in parts)
        {
            partProperties.Add(await CreatePartPropertyAsync(
                part,
                knownDefinitions,
                partSchemaDefinitions,
                fieldSchemaDefinitions,
                nextActiveContentTypes,
                cancellationToken));
        }

        if (partProperties.Count == 0)
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
                .Properties(partProperties.ToArray())
                .AdditionalProperties(true));
    }

    private async ValueTask<(string Name, JsonSchemaBuilder Schema)> CreatePartPropertyAsync(
        ContentTypePartDefinition definition,
        ContentTypeDefinition[] knownDefinitions,
        ILookup<string, IContentPartSchemaDefinition> partSchemaDefinitions,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        HashSet<string> activeContentTypes,
        CancellationToken cancellationToken)
    {
        var fields = definition.PartDefinition?.Fields?
            .Where(field => !string.IsNullOrWhiteSpace(field.Name))
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray() ?? [];
        var fieldProperties = new List<(string Name, JsonSchemaBuilder Schema)>();

        foreach (var field in fields)
        {
            fieldProperties.Add(await CreateFieldPropertyAsync(
                definition,
                field,
                fieldSchemaDefinitions,
                cancellationToken));
        }

        var description = $"The '{definition.Name}' part available on '{definition.ContentTypeDefinition?.Name ?? "the selected content type"}'.";
        var schema = await CreatePartSchemaAsync(
            definition,
            knownDefinitions,
            partSchemaDefinitions,
            fieldSchemaDefinitions,
            activeContentTypes,
            cancellationToken);

        if (fieldProperties.Count == 0)
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
                    .Properties(fieldProperties.ToArray())
                    .AdditionalProperties(true)));
    }

    private async ValueTask<JsonSchemaBuilder> CreatePartSchemaAsync(
        ContentTypePartDefinition definition,
        ContentTypeDefinition[] knownDefinitions,
        ILookup<string, IContentPartSchemaDefinition> partSchemaDefinitions,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        HashSet<string> activeContentTypes,
        CancellationToken cancellationToken)
    {
        var partDefinitionName = definition.PartDefinition?.Name;

        if (string.IsNullOrWhiteSpace(partDefinitionName) || !partSchemaDefinitions.Contains(partDefinitionName))
        {
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true);
        }

        var context = new ContentPartSchemaContext
        {
            ContentTypePartDefinition = definition,
        };
        var fragments = new List<JsonSchemaBuilder>();
        var containedContentProperties = new List<(string Name, JsonSchemaBuilder Schema)>();
        var nextActiveContentTypes = new HashSet<string>(activeContentTypes, StringComparer.OrdinalIgnoreCase);

        foreach (var schemaDefinition in partSchemaDefinitions[partDefinitionName].OrderBy(d => d.GetType().Name, StringComparer.Ordinal))
        {
            fragments.Add(await schemaDefinition.GetPartSchemaAsync(context, cancellationToken));

            if (schemaDefinition is IContainedContentPartSchemaDefinition containedContentPartSchemaDefinition)
            {
                containedContentProperties.Add(await CreateContainedContentPropertyAsync(
                    containedContentPartSchemaDefinition,
                    context,
                    knownDefinitions,
                    partSchemaDefinitions,
                    fieldSchemaDefinitions,
                    nextActiveContentTypes,
                    cancellationToken));
            }
        }

        var schema = MergeFragments(fragments);

        if (containedContentProperties.Count == 0)
        {
            return schema;
        }

        return new JsonSchemaBuilder()
            .AllOf(
                schema,
                new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(containedContentProperties.ToArray())
                    .AdditionalProperties(true));
    }

    private static async ValueTask<(string Name, JsonSchemaBuilder Schema)> CreateFieldPropertyAsync(
        ContentTypePartDefinition partDefinition,
        ContentPartFieldDefinition definition,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        CancellationToken cancellationToken)
    {
        var fieldDefinitionName = definition.FieldDefinition?.Name;

        if (string.IsNullOrWhiteSpace(fieldDefinitionName) || !fieldSchemaDefinitions.Contains(fieldDefinitionName))
        {
            return (definition.Name, new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true));
        }

        var context = new ContentFieldSchemaContext
        {
            ContentPartFieldDefinition = definition,
            ContentTypePartDefinition = partDefinition,
        };
        var fragments = new List<JsonSchemaBuilder>();

        foreach (var schemaDefinition in fieldSchemaDefinitions[fieldDefinitionName].OrderBy(d => d.GetType().Name, StringComparer.Ordinal))
        {
            fragments.Add(await schemaDefinition.GetFieldSchemaAsync(context, cancellationToken));
        }

        return (definition.Name, MergeFragments(fragments));
    }

    private static JsonSchemaBuilder MergeFragments(List<JsonSchemaBuilder> fragments)
    {
        if (fragments.Count == 0)
        {
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true);
        }

        if (fragments.Count == 1)
        {
            return fragments[0];
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AllOf(fragments.ToArray())
            .AdditionalProperties(true);
    }

    private async ValueTask<(string Name, JsonSchemaBuilder Schema)> CreateContainedContentPropertyAsync(
        IContainedContentPartSchemaDefinition containedContentPartSchemaDefinition,
        ContentPartSchemaContext context,
        ContentTypeDefinition[] knownDefinitions,
        ILookup<string, IContentPartSchemaDefinition> partSchemaDefinitions,
        ILookup<string, IContentFieldSchemaDefinition> fieldSchemaDefinitions,
        HashSet<string> activeContentTypes,
        CancellationToken cancellationToken)
    {
        var allowedContentTypes = await containedContentPartSchemaDefinition.GetContainedContentTypesAsync(
            context,
            knownDefinitions,
            cancellationToken);
        var allowedNames = NormalizeContentTypes(allowedContentTypes);
        JsonSchemaBuilder itemSchema;

        if (allowedNames.Length == 0)
        {
            itemSchema = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .AdditionalProperties(true);
        }
        else
        {
            var matchingDefinitions = knownDefinitions
                .Where(contentTypeDefinition => allowedNames.Contains(contentTypeDefinition.Name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var nestedDefinitions = matchingDefinitions
                .Where(contentTypeDefinition => !activeContentTypes.Contains(contentTypeDefinition.Name))
                .ToArray();

            if (nestedDefinitions.Length == 0)
            {
                itemSchema = CreateContentItemSchema(allowedNames);
            }
            else
            {
                itemSchema = await CreateComposedContentItemSchemaAsync(
                    nestedDefinitions,
                    knownDefinitions,
                    partSchemaDefinitions,
                    fieldSchemaDefinitions,
                    activeContentTypes,
                    cancellationToken);

                if (allowedNames.Length > nestedDefinitions.Length)
                {
                    itemSchema = CreateContentItemSchema(allowedNames).AllOf(itemSchema);
                }
            }
        }

        return (containedContentPartSchemaDefinition.NestedItemsPropertyName, new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(itemSchema));
    }

    private static ContentTypeDefinition[] NormalizeContentTypeDefinitions(IEnumerable<ContentTypeDefinition> contentTypeDefinitions)
        => contentTypeDefinitions?
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray() ?? [];

    private static JsonSchemaBuilder CreateContentItemSchema(IReadOnlyList<string> contentTypes)
    {
        var contentTypeSchema = CreateContentTypeValueSchema(contentTypes);

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
                    .Description("The author of the content item."))
            )
            .Required("ContentType")
            .AdditionalProperties(true);
    }

    private static string[] NormalizeContentTypes(IEnumerable<string> contentTypes)
        => contentTypes?
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray() ?? [];

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
}
