using CrestApps.OrchardCore.NLWeb.Models;

namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Turns the items the retrievers found into results: loads them, drops the ones the caller may not see, and
/// describes each one as a schema.org object.
/// </summary>
public interface INLWebResultResolver
{
    /// <summary>
    /// Resolves the candidates into results, in the order of the candidates.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <param name="candidates">The candidates, best first.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The results the caller may see.</returns>
    Task<IReadOnlyList<NLWebResult>> ResolveAsync(NLWebAskContext context, IReadOnlyList<NLWebCandidate> candidates, CancellationToken cancellationToken);
}
