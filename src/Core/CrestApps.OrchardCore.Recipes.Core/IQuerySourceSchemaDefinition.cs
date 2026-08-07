using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries;

namespace CrestApps.OrchardCore.Recipes.Core;

/// <summary>
/// Produces the JSON schema and metadata that describe a single query source inside the <c>Queries</c>
/// array of the <c>Queries</c> recipe step.
/// </summary>
/// <remarks>
/// Implement this interface when a module contributes a custom query source and wants the generated recipe
/// schema to describe the source's members. Registering the implementation as
/// <see cref="IQuerySourceSchemaDefinition"/> is enough for the <c>Queries</c> recipe step to pick it up.
/// Prefer deriving from
/// <see cref="CrestApps.OrchardCore.Recipes.Core.Schemas.Queries.QuerySourceSchemaDefinitionBase"/>, which
/// handles the standard query envelope.
/// </remarks>
public interface IQuerySourceSchemaDefinition
{
    /// <summary>
    /// Gets the query source name that this definition describes, matching the <c>Source</c> discriminator,
    /// for example <c>Sql</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Builds the schema and metadata describing the query source.
    /// </summary>
    /// <param name="context">The context describing the source being documented.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    ValueTask<QuerySourceSchema> GetSourceSchemaAsync(QuerySourceSchemaContext context, CancellationToken cancellationToken = default);
}
