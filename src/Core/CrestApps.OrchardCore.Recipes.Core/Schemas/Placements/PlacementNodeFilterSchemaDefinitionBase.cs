using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Placements;

/// <summary>
/// Provides the standard implementation surface for placement node filter schema definitions.
/// </summary>
/// <remarks>
/// Derive from this class to describe a placement node filter in the <c>Placements</c> recipe step.
/// Implementations only supply the filter key and the schema of the value the filter accepts; the schema
/// service adds the filter to the placement node under its key.
/// </remarks>
public abstract class PlacementNodeFilterSchemaDefinitionBase : IPlacementNodeFilterSchemaDefinition
{
    /// <inheritdoc />
    public abstract string Key { get; }

    /// <summary>
    /// Gets the human readable filter title. Returns <see langword="null"/> when no title is provided.
    /// </summary>
    protected virtual string DisplayText => null;

    /// <summary>
    /// Gets a description explaining what the filter matches.
    /// </summary>
    protected virtual string Description => null;

    ValueTask<PlacementNodeFilterSchema> IPlacementNodeFilterSchemaDefinition.GetFilterSchemaAsync(
        PlacementNodeFilterSchemaContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return BuildFilterSchemaAsync(context, cancellationToken);
    }

    /// <summary>
    /// Builds the schema describing the filter. Override this method when the schema requires asynchronous work.
    /// </summary>
    /// <param name="context">The context describing the filter being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    protected virtual ValueTask<PlacementNodeFilterSchema> BuildFilterSchemaAsync(
        PlacementNodeFilterSchemaContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildFilterSchemaCore(context));

    /// <summary>
    /// Builds the schema of the filter value, added to the placement node under the filter key.
    /// </summary>
    /// <param name="context">The context describing the filter being documented.</param>
    protected abstract JsonSchemaBuilder GetValueSchema(PlacementNodeFilterSchemaContext context);

    /// <summary>
    /// Assembles the filter schema from the declared metadata and value schema.
    /// </summary>
    /// <param name="context">The context describing the filter being documented.</param>
    protected virtual PlacementNodeFilterSchema BuildFilterSchemaCore(PlacementNodeFilterSchemaContext context)
    {
        return new PlacementNodeFilterSchema
        {
            DisplayText = DisplayText,
            Description = Description,
            ValueSchema = GetValueSchema(context),
        };
    }
}
