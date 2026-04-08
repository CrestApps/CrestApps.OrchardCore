using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing.Models;

namespace CrestApps.Core.AI.Memory;

public interface IMemoryVectorSearchService
{
    Task<IEnumerable<AIMemorySearchResult>> SearchAsync(
        SearchIndexProfile indexProfile,
        float[] embedding,
        string userId,
        int topN,
        CancellationToken cancellationToken = default);
}
