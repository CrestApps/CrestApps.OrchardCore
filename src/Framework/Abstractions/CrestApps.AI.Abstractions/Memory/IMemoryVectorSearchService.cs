using CrestApps.AI.Models;
using CrestApps.Infrastructure.Indexing.Models;

namespace CrestApps.AI.Memory;

public interface IMemoryVectorSearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        SearchIndexProfile indexProfile,
        float[] embedding,
        string userId,
        int topN,
        CancellationToken cancellationToken = default);
}
