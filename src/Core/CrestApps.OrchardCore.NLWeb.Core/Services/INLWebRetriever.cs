using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Finds the items that match a question, such as with a keyword index or a vector index. When several retrievers
/// are enabled, their lists are merged with reciprocal rank fusion.
/// </summary>
public interface INLWebRetriever
{
    /// <summary>
    /// Gets the name of the retriever.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks whether the retriever can search for this question, for example whether it is configured.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <returns><see langword="true"/> when the retriever should run.</returns>
    Task<bool> IsEnabledAsync(NLWebAskContext context);

    /// <summary>
    /// Finds the items that match the question, best first.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <param name="count">The most items to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The items found.</returns>
    Task<IReadOnlyList<NLWebCandidate>> RetrieveAsync(NLWebAskContext context, int count, CancellationToken cancellationToken);
}
