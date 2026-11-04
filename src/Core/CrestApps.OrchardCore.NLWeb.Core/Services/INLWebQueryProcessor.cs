namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Prepares a question before the site searches for it: applies the site's limits, rewrites the query with the
/// conversation's context, guesses the type of item asked for, or decides that more information is needed.
/// </summary>
public interface INLWebQueryProcessor
{
    /// <summary>
    /// Gets the order the processor runs in. Lower values run first.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Prepares the question. A processor can end the pipeline by setting <see cref="NLWebAskContext.Failure"/> or
    /// <see cref="NLWebAskContext.Elicitation"/>.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the question is prepared.</returns>
    Task ProcessAsync(NLWebAskContext context, CancellationToken cancellationToken);
}
