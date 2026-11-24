using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.OrchardCore.AI.Core.DataSources;

/// <summary>
/// Trims the chunks a data source search returned before they reach a model, for the person and the AI profile of the
/// request. Every retrieval path (the search before the turn, the search tool, the search tool instances and
/// anything else that searches a knowledge base index) reads its chunks through the provider's
/// <see cref="CrestApps.Core.Infrastructure.Indexing.DataSources.IDataSourceContentManager"/>, which AI Data Sources
/// wraps so that every registered filter runs on every search.
/// </summary>
/// <remarks>
/// The static filter of a data source or profile narrows the search inside the index. A result filter decides per
/// request, after the search, with services the index does not know about (permissions, enrollments, records). The
/// search asks the index for more chunks than the caller wants when filters are registered, so a trimmed list still
/// fills up; see <see cref="AIDataSourceResultFilterOptions"/>.
/// </remarks>
public interface IAIDataSourceResultFilter
{
    /// <summary>
    /// Keeps the chunks the request may receive.
    /// </summary>
    /// <param name="context">The request: the person, the AI profile or chat interaction, and the data source.</param>
    /// <param name="results">The chunks the index returned, best first.</param>
    /// <returns>The chunks to keep, in the same order.</returns>
    Task<IReadOnlyList<DataSourceSearchResult>> FilterAsync(AIDataSourceResultFilterContext context, IReadOnlyList<DataSourceSearchResult> results);
}
