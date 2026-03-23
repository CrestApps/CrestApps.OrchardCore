using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

public interface IAIMemoryStore : ICatalog<AIMemoryEntry>
{
    Task<int> CountByUserAsync(string userId);

    Task<AIMemoryEntry> FindByUserAndNameAsync(string userId, string name);

    Task<IReadOnlyCollection<AIMemoryEntry>> GetByUserAsync(string userId, int limit = 100);
}
