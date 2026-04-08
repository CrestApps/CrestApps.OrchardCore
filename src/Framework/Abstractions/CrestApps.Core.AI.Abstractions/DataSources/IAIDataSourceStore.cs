using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.DataSources;

/// <summary>
/// Store for managing <see cref="AIDataSource"/> records.
/// </summary>
public interface IAIDataSourceStore : ICatalog<AIDataSource>
{
}
