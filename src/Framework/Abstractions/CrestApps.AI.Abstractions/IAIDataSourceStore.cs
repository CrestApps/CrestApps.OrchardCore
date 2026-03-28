using CrestApps.AI.Models;
using CrestApps.Services;

namespace CrestApps.AI;

/// <summary>
/// Store for managing <see cref="AIDataSource"/> records.
/// </summary>
public interface IAIDataSourceStore : ICatalog<AIDataSource>
{
}
