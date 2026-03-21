using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIMemoryStore : ICatalog<AIMemoryEntry>
{
    Task<int> CountByUserAsync(string userId);

    Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name);

    Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100);
}
