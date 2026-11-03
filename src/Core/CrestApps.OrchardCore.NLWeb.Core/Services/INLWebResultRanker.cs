namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Orders the results by how well they answer the question, and can drop the ones that do not answer it.
/// </summary>
public interface INLWebResultRanker
{
    /// <summary>
    /// Ranks <see cref="NLWebAskContext.Results"/> in place.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the results are ranked.</returns>
    Task RankAsync(NLWebAskContext context, CancellationToken cancellationToken);
}
