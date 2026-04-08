using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Memory;

/// <summary>
/// Searches the current user's durable memory entries for relevant context.
/// Hosts can provide their own backing implementation while reusing the shared
/// orchestration and tool behavior.
/// </summary>
public interface IAIMemorySearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        string userId,
        IEnumerable<string> queries,
        int? requestedTopN,
        CancellationToken cancellationToken = default);
}
