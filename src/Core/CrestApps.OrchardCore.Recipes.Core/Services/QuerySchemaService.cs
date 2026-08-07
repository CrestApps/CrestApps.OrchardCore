using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the query source schemas from the registered <see cref="IQuerySourceSchemaDefinition"/>
/// contributions into the schema used by the <c>Queries</c> recipe step.
/// </summary>
public sealed class QuerySchemaService : IQuerySchemaService
{
    private readonly IEnumerable<IQuerySourceSchemaDefinition> _sourceDefinitions;
    private readonly IRecipeSchemaExampleService _exampleService;

    private IReadOnlyList<QuerySourceDescriptor> _cachedSourceDescriptors;
    private JsonSchemaBuilder _cachedQuerySchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuerySchemaService"/> class.
    /// </summary>
    /// <param name="sourceDefinitions">The registered query source schema definitions.</param>
    /// <param name="exampleService">The service that supplies live tenant example values.</param>
    public QuerySchemaService(
        IEnumerable<IQuerySourceSchemaDefinition> sourceDefinitions,
        IRecipeSchemaExampleService exampleService)
    {
        _sourceDefinitions = sourceDefinitions;
        _exampleService = exampleService;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<QuerySourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSourceDescriptors is not null)
        {
            return _cachedSourceDescriptors;
        }

        var definitions = _sourceDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<QuerySourceDescriptor>();

        var examples = await _exampleService.GetExamplesAsync(cancellationToken);

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var context = new QuerySourceSchemaContext
            {
                SourceName = definition.Name,
                Examples = examples,
            };

            var schema = await definition.GetSourceSchemaAsync(context, cancellationToken);

            descriptors.Add(new QuerySourceDescriptor
            {
                Name = definition.Name,
                DisplayText = schema?.DisplayText,
                Description = schema?.Description,
                Properties = schema?.Properties ?? [],
                RequiredProperties = schema?.RequiredProperties ?? [],
            });
        }

        _cachedSourceDescriptors = descriptors;

        return _cachedSourceDescriptors;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetQuerySchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedQuerySchema is not null)
        {
            return _cachedQuerySchema;
        }

        var descriptors = await GetSourceDescriptorsAsync(cancellationToken);

        _cachedQuerySchema = BuildQuerySchema(descriptors);

        return _cachedQuerySchema;
    }

    private static JsonSchemaBuilder BuildQuerySchema(IReadOnlyList<QuerySourceDescriptor> descriptors)
    {
        var sourceNames = descriptors
            .Select(descriptor => descriptor.Name)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>();

        foreach (var descriptor in descriptors)
        {
            var thenProperties = new List<(string Name, JsonSchemaBuilder Schema)>
            {
                ("Source", new JsonSchemaBuilder()
                    .Const(descriptor.Name)
                    .Description(BuildSourceDescription(descriptor))),
            };

            thenProperties.AddRange(descriptor.Properties);

            var thenBuilder = new JsonSchemaBuilder()
                .Properties(thenProperties.ToArray());

            if (descriptor.RequiredProperties.Count > 0)
            {
                thenBuilder = thenBuilder.Required(descriptor.RequiredProperties.ToArray());
            }

            conditionals.Add(new JsonSchemaBuilder()
                .If(new JsonSchemaBuilder()
                    .Properties(("Source", new JsonSchemaBuilder().Const(descriptor.Name)))
                    .Required("Source"))
                .Then(thenBuilder));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The technical name of the query. Must be unique on the tenant.")),
                ("Source", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(sourceNames)
                    .Description("The query source provider name, such as Sql, Lucene or Elasticsearch. The well known members of the query depend on this value.")),
                ("Schema", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The optional return schema of the query, used to shape the results when content items are not returned. It supports script expressions such as [js: ...].")),
                ("ReturnContentItems", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether the query returns full content items instead of the raw source results.")))
            .Required("Name", "Source")
            .AdditionalProperties(true)
            .Description("A query to create or update.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static string BuildSourceDescription(QuerySourceDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} query.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed 'Source' for the '{descriptor.Name}' query source.");

        return string.Join(" ", parts);
    }
}
