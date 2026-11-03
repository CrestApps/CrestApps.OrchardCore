using System.Text;
using CrestApps.OrchardCore.NLWeb.Models;
using OrchardCore.Indexing;
using OrchardCore.Search.Abstractions;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Finds content items with the keyword search of Orchard Core's search providers (Lucene, Elasticsearch or Azure AI
/// Search), in the search index the NLWeb settings select. It is registered only when Orchard Core's Search feature
/// is enabled, because the providers register their search service only then.
/// </summary>
internal sealed class IndexSearchNLWebRetriever : INLWebRetriever
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IEnumerable<ISearchService> _searchServices;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexSearchNLWebRetriever"/> class.
    /// </summary>
    /// <param name="indexProfileStore">The search index store.</param>
    /// <param name="searchServices">The search services of the enabled search providers.</param>
    public IndexSearchNLWebRetriever(
        IIndexProfileStore indexProfileStore,
        IEnumerable<ISearchService> searchServices)
    {
        _indexProfileStore = indexProfileStore;
        _searchServices = searchServices;
    }

    /// <inheritdoc/>
    public string Name => NLWebConstants.Retrievers.Index;

    /// <inheritdoc/>
    public Task<bool> IsEnabledAsync(NLWebAskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(context.Properties.ContainsKey(NLWebSettingsQueryProcessor.IndexProfileNameKey));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebAskContext context, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var indexName = context.Properties[NLWebSettingsQueryProcessor.IndexProfileNameKey] as string;
        var indexProfile = await _indexProfileStore.FindByNameAsync(indexName);

        if (indexProfile is null)
        {
            return [];
        }

        var searchService = _searchServices.FirstOrDefault(service => string.Equals(service.Name, indexProfile.ProviderName, StringComparison.Ordinal));
        var terms = ToSearchTerms(context.QueryText);

        if (searchService is null || string.IsNullOrEmpty(terms))
        {
            return [];
        }

        var result = await searchService.SearchAsync(indexProfile, terms, 0, count);

        if (!result.Success || result.ContentItemIds is null)
        {
            return [];
        }

        var candidates = new List<NLWebCandidate>();

        foreach (var contentItemId in result.ContentItemIds)
        {
            candidates.Add(new NLWebCandidate(contentItemId, score: null, Name));
        }

        return candidates;
    }

    /// <summary>
    /// Turns a natural language question into search terms. The characters that query parsers read as operators,
    /// such as the question mark (a wildcard in Lucene), are replaced with spaces, so a question cannot fail to parse.
    /// </summary>
    /// <param name="text">The question.</param>
    /// <returns>The search terms.</returns>
    internal static string ToSearchTerms(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var previousWasSpace = false;

        foreach (var character in text)
        {
            var keep = char.IsLetterOrDigit(character) || character == '\'' || character == '.' || character == '@';

            if (keep)
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
