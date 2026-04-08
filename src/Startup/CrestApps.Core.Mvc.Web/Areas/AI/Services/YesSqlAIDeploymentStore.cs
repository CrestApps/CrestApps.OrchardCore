using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Mvc.Web.Areas.AI.Indexes;

namespace CrestApps.Core.Mvc.Web.Areas.AI.Services;

public sealed class YesSqlAIDeploymentStore : NamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex>, IAIDeploymentStore
{
    public YesSqlAIDeploymentStore(YesSql.ISession session)
        : base(session)
    {
    }
}
