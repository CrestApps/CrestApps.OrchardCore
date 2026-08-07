using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the placement node filter schemas from the registered
/// <see cref="IPlacementNodeFilterSchemaDefinition"/> contributions into the placement node schema used by
/// the <c>Placements</c> recipe step.
/// </summary>
public sealed class PlacementSchemaService : IPlacementSchemaService
{
    private readonly IEnumerable<IPlacementNodeFilterSchemaDefinition> _filterDefinitions;

    private IReadOnlyList<PlacementNodeFilterDescriptor> _cachedFilterDescriptors;
    private JsonSchemaBuilder _cachedPlacementNodeSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlacementSchemaService"/> class.
    /// </summary>
    /// <param name="filterDefinitions">The registered placement node filter schema definitions.</param>
    public PlacementSchemaService(IEnumerable<IPlacementNodeFilterSchemaDefinition> filterDefinitions)
    {
        _filterDefinitions = filterDefinitions;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<PlacementNodeFilterDescriptor>> GetFilterDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedFilterDescriptors is not null)
        {
            return _cachedFilterDescriptors;
        }

        var definitions = _filterDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Key))
            .GroupBy(definition => definition.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<PlacementNodeFilterDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Key, StringComparer.Ordinal))
        {
            var context = new PlacementNodeFilterSchemaContext
            {
                Key = definition.Key,
            };

            var schema = await definition.GetFilterSchemaAsync(context, cancellationToken);

            descriptors.Add(new PlacementNodeFilterDescriptor
            {
                Key = definition.Key,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                ValueSchema = schema?.ValueSchema,
            });
        }

        _cachedFilterDescriptors = descriptors;

        return _cachedFilterDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetPlacementNodeSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPlacementNodeSchema is not null)
        {
            return _cachedPlacementNodeSchema;
        }

        var descriptors = await GetFilterDescriptorsAsync(cancellationToken);

        _cachedPlacementNodeSchema = BuildPlacementNodeSchema(descriptors);

        return _cachedPlacementNodeSchema;
    }

    private static JsonSchemaBuilder BuildPlacementNodeSchema(IReadOnlyList<PlacementNodeFilterDescriptor> descriptors)
    {
        var properties = new List<(string Name, JsonSchemaBuilder Schema)>
        {
            ("place", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The placement location, such as 'Content:1', a zone and position, or '-' to hide the shape.")),
            ("displayType", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The display type the placement applies to, such as Detail, Summary or Edit. Leave it empty to apply to every display type.")),
            ("differentiator", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The differentiator that narrows the placement to a specific named shape, such as a specific field name.")),
            ("shape", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The shape type the placement renders instead of the default shape.")),
            ("alternates", new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .Description("The alternates added to the shape so a template can override its rendering.")),
            ("wrappers", new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .Description("The wrapper shapes rendered around the shape.")),
        };

        foreach (var descriptor in descriptors)
        {
            var valueSchema = descriptor.ValueSchema ?? new JsonSchemaBuilder();

            if (!string.IsNullOrWhiteSpace(descriptor.Description))
            {
                valueSchema = valueSchema.Description(descriptor.Description);
            }

            properties.Add((descriptor.Key, valueSchema));
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(properties.ToArray())
            .Required("place")
            .AdditionalProperties(true)
            .Description("A placement node describing where and how a shape is rendered, optionally narrowed by filters.");
    }
}
