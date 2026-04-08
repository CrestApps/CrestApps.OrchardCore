using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIDataSourceStore : DocumentCatalog<AIDataSource>, IAIDataSourceStore
{
    public EntityCoreAIDataSourceStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }
}
