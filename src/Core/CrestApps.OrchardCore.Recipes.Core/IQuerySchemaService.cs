using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Builds JSON schemas describing the query sources available on the current tenant, used by the
/// <c>Queries</c> recipe step to describe each entry of its <c>Queries</c> array.
/// </summary>
public interface IQuerySchemaService
{
    /// <summary>
    /// Gets a descriptor for every query source contributed through an
    /// <see cref="IQuerySourceSchemaDefinition"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<IReadOnlyList<QuerySourceDescriptor>> GetSourceDescriptorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the schema describing a single entry of the <c>Queries</c> array, keyed on the <c>Source</c>
    /// discriminator.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<JsonSchemaBuilder> GetQuerySchemaAsync(CancellationToken cancellationToken = default);
}
