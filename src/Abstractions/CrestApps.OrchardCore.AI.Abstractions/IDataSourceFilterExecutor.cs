namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Executes a filter query against a source index and returns matching document keys.
/// Implementations are registered as keyed services using the provider name.
/// Used in two-phase RAG search: first filter by user-defined criteria, then vector search within results.
/// </summary>
public interface IDataSourceFilterExecutor
{
    /// <summary>
    /// Executes a filter query against the specified index and returns the matching document keys.
    /// </summary>
    /// <param name="indexName">The name of the source index to filter.</param>
    /// <param name="filter">The provider-specific filter expression (e.g., Elasticsearch DSL JSON or OData filter).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching document keys, or null if the filter could not be executed.</returns>
    Task<IEnumerable<string>> ExecuteAsync(
        string indexName,
        string filter,
        CancellationToken cancellationToken = default);
}
