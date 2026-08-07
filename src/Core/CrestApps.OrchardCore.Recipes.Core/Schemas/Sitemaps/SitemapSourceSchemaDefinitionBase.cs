using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps;

/// <summary>
/// Provides the standard implementation surface for sitemap source schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a sitemap source in the <c>Sitemaps</c> recipe step. Implementations
/// only supply the source name, its type discriminator and the members it accepts; the schema service
/// assembles the shared <c>$type</c> and <c>Id</c> members.
/// </remarks>
public abstract class SitemapSourceSchemaDefinitionBase : ISitemapSourceSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string TypeDiscriminator { get; }

    /// <summary>
    /// Gets the human readable source title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the source contributes to the sitemap.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    ValueTask<SitemapSourceSchema> ISitemapSourceSchemaDefinition.GetSourceSchemaAsync(
        SitemapSourceSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildSourceSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the source. Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<SitemapSourceSchema> BuildSourceSchemaAsync(
        SitemapSourceSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildSourceSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the source, beyond the shared members.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context);

    /// <summary>
    /// Assembles the source schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected virtual SitemapSourceSchema BuildSourceSchemaCore(SitemapSourceSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new SitemapSourceSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
        };
    }
}
