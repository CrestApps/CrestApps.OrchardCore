using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;

/// <summary>
/// Provides the standard implementation surface for query source schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a query source in the <c>Queries</c> recipe step. Implementations only
/// supply the source name and the members it accepts; the schema service assembles the shared members, such
/// as <c>Name</c>, <c>Source</c>, <c>Schema</c> and <c>ReturnContentItems</c>.
/// </remarks>
public abstract class QuerySourceSchemaDefinitionBase : IQuerySourceSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// Gets the human readable source title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the source runs.
    /// </summary>
    protected virtual string Description => null;

    /// <summary>
    /// Gets the names of the properties that must be provided in addition to the shared members.
    /// </summary>
    protected virtual IEnumerable<string> RequiredProperties => [];

    ValueTask<QuerySourceSchema> IQuerySourceSchemaDefinition.GetSourceSchemaAsync(
        QuerySourceSchemaContext context,
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
    protected virtual ValueTask<QuerySourceSchema> BuildSourceSchemaAsync(
        QuerySourceSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildSourceSchemaCore(context));

    /// <summary>
    /// Builds the property definitions accepted by the source, beyond the shared members.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected abstract IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(QuerySourceSchemaContext context);

    /// <summary>
    /// Assembles the source schema from the declared metadata and property definitions.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    protected virtual QuerySourceSchema BuildSourceSchemaCore(QuerySourceSchemaContext context)
    {
        var properties = GetPropertyDefinitions(context)?.ToArray() ?? [];

        return new QuerySourceSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            Properties = properties,
            RequiredProperties = RequiredProperties?.ToArray() ?? [],
        };
    }
}
