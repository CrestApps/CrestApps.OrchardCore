using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.Data.EntityCore.Services;

public sealed class EntityCoreAIDeploymentStore : NamedSourceDocumentCatalog<AIDeployment>, IAIDeploymentStore
{
    public EntityCoreAIDeploymentStore(CrestAppsEntityDbContext dbContext)
        : base(dbContext)
    {
    }
}
