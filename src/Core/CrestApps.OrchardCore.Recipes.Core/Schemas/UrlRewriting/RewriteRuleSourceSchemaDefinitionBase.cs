using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting;

/// <summary>
/// Provides the standard implementation surface for rewrite rule source schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a rewrite rule source in the <c>UrlRewriting</c> recipe step.
/// Implementations only supply the source name and the members it accepts; the schema service assembles the
/// shared members, such as <c>Id</c>, <c>Source</c>, <c>Name</c> and <c>Order</c>.
/// </remarks>
public abstract class RewriteRuleSourceSchemaDefinitionBase : IRewriteRuleSourceSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// Gets the human readable source title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the source does.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    ValueTask<RewriteRuleSourceSchema> IRewriteRuleSourceSchemaDefinition.GetSourceSchemaAsync(
        RewriteRuleSourceSchemaContext context,
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
    protected virtual ValueTask<RewriteRuleSourceSchema> BuildSourceSchemaAsync(
        RewriteRuleSourceSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildSourceSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the source, beyond the shared members.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(RewriteRuleSourceSchemaContext context);

    /// <summary>
    /// Assembles the source schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected virtual RewriteRuleSourceSchema BuildSourceSchemaCore(RewriteRuleSourceSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new RewriteRuleSourceSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
        };
    }
}
