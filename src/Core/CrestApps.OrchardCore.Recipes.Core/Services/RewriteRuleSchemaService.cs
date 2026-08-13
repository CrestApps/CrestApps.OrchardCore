using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the rewrite rule source schemas from the registered
/// <see cref="IRewriteRuleSourceSchemaDefinition"/> contributions into the schema used by the
/// <c>UrlRewriting</c> recipe step.
/// </summary>
public sealed class RewriteRuleSchemaService : IRewriteRuleSchemaService
{
    private readonly IEnumerable<IRewriteRuleSourceSchemaDefinition> _sourceDefinitions;

    private IReadOnlyList<RewriteRuleSourceDescriptor> _cachedSourceDescriptors;
    private JsonSchemaBuilder _cachedRuleSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="RewriteRuleSchemaService"/> class.
    /// </summary>
    /// <param name="sourceDefinitions">The registered rewrite rule source schema definitions.</param>
    public RewriteRuleSchemaService(IEnumerable<IRewriteRuleSourceSchemaDefinition> sourceDefinitions)
    {
        _sourceDefinitions = sourceDefinitions;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RewriteRuleSourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSourceDescriptors is not null)
        {
            return _cachedSourceDescriptors;
        }

        var definitions = _sourceDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<RewriteRuleSourceDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var context = new RewriteRuleSourceSchemaContext
            {
                SourceName = definition.Name,
            };

            var schema = await definition.GetSourceSchemaAsync(context, cancellationToken);

            descriptors.Add(new RewriteRuleSourceDescriptor
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
    public async ValueTask<JsonSchemaBuilder> GetRuleSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedRuleSchema is not null)
        {
            return _cachedRuleSchema;
        }

        var descriptors = await GetSourceDescriptorsAsync(cancellationToken);

        _cachedRuleSchema = BuildRuleSchema(descriptors);

        return _cachedRuleSchema;
    }

    private static JsonSchemaBuilder BuildRuleSchema(IReadOnlyList<RewriteRuleSourceDescriptor> descriptors)
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
                ("Id", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The identifier of an existing rule to update. Leave it empty to create a new rule.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The display name of the rewrite rule.")),
                ("Source", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(sourceNames)
                    .Description("The rewrite rule source provider name, such as Redirect or Rewrite. The well known members of the rule depend on this value.")),
                ("Order", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Description("The order in which the rule is evaluated relative to the other rules. Lower values run first.")))
            .Required("Source")
            .AdditionalProperties(true)
            .Description("A URL rewrite rule to create or update.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static string BuildSourceDescription(RewriteRuleSourceDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} rule.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed 'Source' for the '{descriptor.Name}' rewrite rule source.");

        return string.Join(" ", parts);
    }
}
