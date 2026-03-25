using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

public interface IAIProfileStore : INamedCatalog<AIProfile>
{
    /// <summary>
    /// Asynchronously retrieves a collection of AI profiles of the specified type
    /// using an efficient index query rather than loading all profiles.
    /// </summary>
    /// <param name="type">The type of AI profiles to retrieve.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
    /// The result is a read-only collection of <see cref="AIProfile"/> matching the specified type.
    /// </returns>
    ValueTask<IReadOnlyCollection<AIProfile>> GetByTypeAsync(AIProfileType type);
}
