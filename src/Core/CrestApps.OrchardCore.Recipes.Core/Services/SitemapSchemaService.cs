using CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Composes the sitemap source schemas from the registered <see cref="ISitemapSourceSchemaDefinition"/>
/// contributions into the schema used by the <c>Sitemaps</c> recipe step.
/// </summary>
public sealed class SitemapSchemaService : ISitemapSchemaService
{
    private static readonly string[] _sitemapTypeDiscriminators =
    [
        "OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions",
        "OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps",
    ];

    private readonly IEnumerable<ISitemapSourceSchemaDefinition> _sourceDefinitions;

    private IReadOnlyList<SitemapSourceDescriptor> _cachedSourceDescriptors;
    private JsonSchemaBuilder _cachedSourceSchema;
    private JsonSchemaBuilder _cachedSitemapSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="SitemapSchemaService"/> class.
    /// </summary>
    /// <param name="sourceDefinitions">The registered sitemap source schema definitions.</param>
    public SitemapSchemaService(IEnumerable<ISitemapSourceSchemaDefinition> sourceDefinitions)
    {
        _sourceDefinitions = sourceDefinitions;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SitemapSourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSourceDescriptors is not null)
        {
            return _cachedSourceDescriptors;
        }

        var definitions = _sourceDefinitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        var descriptors = new List<SitemapSourceDescriptor>();

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.Ordinal))
        {
            var context = new SitemapSourceSchemaContext
            {
                SourceName = definition.Name,
            };

            var schema = await definition.GetSourceSchemaAsync(context, cancellationToken);

            descriptors.Add(new SitemapSourceDescriptor
            {
                Name = definition.Name,
                TypeDiscriminator = definition.TypeDiscriminator,
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
    public async ValueTask<JsonSchemaBuilder> GetSourceSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSourceSchema is not null)
        {
            return _cachedSourceSchema;
        }

        var descriptors = await GetSourceDescriptorsAsync(cancellationToken);

        _cachedSourceSchema = BuildSourceSchema(descriptors);

        return _cachedSourceSchema;
    }

    /// <inheritdoc />
    public async ValueTask<JsonSchemaBuilder> GetSitemapSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSitemapSchema is not null)
        {
            return _cachedSitemapSchema;
        }

        var sourceSchema = await GetSourceSchemaAsync(cancellationToken);

        _cachedSitemapSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(_sitemapTypeDiscriminators)
                    .Description("The polymorphic sitemap type discriminator. Use 'OrchardCore.Sitemaps.Models.Sitemap, OrchardCore.Sitemaps.Abstractions' for a standard sitemap or 'OrchardCore.Sitemaps.Models.SitemapIndex, OrchardCore.Sitemaps' for a sitemap index.")),
                ("SitemapId", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A stable unique identifier for the sitemap. When omitted, one is generated when the recipe runs.")),
                ("Name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The sitemap name.")),
                ("Enabled", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Description("Whether the sitemap is included in routing. Defaults to true.")),
                ("Path", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The public path the sitemap is served from, for example sitemap.xml.")),
                ("SitemapSources", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(sourceSchema)
                    .Description("The sources that contribute entries to the sitemap.")))
            .Required("$type")
            .AdditionalProperties(true)
            .Description("A sitemap or sitemap index to create or update.");

        return _cachedSitemapSchema;
    }

    private static JsonSchemaBuilder BuildSourceSchema(IReadOnlyList<SitemapSourceDescriptor> descriptors)
    {
        var discriminators = descriptors
            .Select(descriptor => descriptor.TypeDiscriminator)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var conditionals = new List<JsonSchemaBuilder>();

        foreach (var descriptor in descriptors)
        {
            var thenProperties = new List<(string Name, JsonSchemaBuilder Schema)>
            {
                ("$type", new JsonSchemaBuilder()
                    .Const(descriptor.TypeDiscriminator)
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
                    .Properties(("$type", new JsonSchemaBuilder().Const(descriptor.TypeDiscriminator)))
                    .Required("$type"))
                .Then(thenBuilder));
        }

        var builder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("$type", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(discriminators)
                    .Description("The polymorphic sitemap source type discriminator, for example OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions. Required so the source deserializes correctly.")),
                ("Id", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("A stable unique identifier for the source. When omitted, one is generated when the recipe runs.")))
            .Required("$type")
            .AdditionalProperties(true)
            .Description("A source that contributes entries to the sitemap.");

        if (conditionals.Count > 0)
        {
            builder = builder.AllOf(conditionals.ToArray());
        }

        return builder;
    }

    private static string BuildSourceDescription(SitemapSourceDescriptor descriptor)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(descriptor.DisplayText))
        {
            parts.Add($"{descriptor.DisplayText} source.");
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Description))
        {
            parts.Add(descriptor.Description.EndsWith('.') ? descriptor.Description : $"{descriptor.Description}.");
        }

        parts.Add($"Fixed '$type' for the '{descriptor.Name}' source.");

        return string.Join(" ", parts);
    }
}
