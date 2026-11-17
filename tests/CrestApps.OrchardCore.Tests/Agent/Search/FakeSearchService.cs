using OrchardCore.Indexing.Models;
using OrchardCore.Search.Abstractions;

namespace CrestApps.OrchardCore.Tests.Agent.Search;

/// <summary>
/// Stands in for the search service of a provider in a tool test: it answers a search with the test's result and
/// records what was searched for.
/// </summary>
internal sealed class FakeSearchService : ISearchService
{
    /// <summary>
    /// Gets or sets what a search returns.
    /// </summary>
    public SearchResult Result { get; set; } = new()
    {
        Success = true,
        ContentItemIds = [],
    };

    /// <summary>
    /// Gets the searches that were run: the name of the index, the terms and the two paging values.
    /// </summary>
    public List<(string Index, string Terms, int Start, int Size)> Searches { get; } = [];

    public string Name => "Lucene";

    public Task<SearchResult> SearchAsync(IndexProfile index, string term, int start, int size)
    {
        Searches.Add((index.Name, term, start, size));

        return Task.FromResult(Result);
    }
}
