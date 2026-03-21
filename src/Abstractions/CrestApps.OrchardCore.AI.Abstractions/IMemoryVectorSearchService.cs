using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI;

public interface IMemoryVectorSearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string userId,
        int topN,
        CancellationToken cancellationToken = default);
}
