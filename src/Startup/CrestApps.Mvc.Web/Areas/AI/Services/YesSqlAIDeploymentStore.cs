using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Services;
using CrestApps.Mvc.Web.Areas.AI.Indexes;
using YesSql;

namespace CrestApps.Mvc.Web.Areas.AI.Services;

public sealed class YesSqlAIDeploymentStore : NamedSourceDocumentCatalog<AIDeployment, AIDeploymentIndex>, IAIDeploymentStore
{
    public YesSqlAIDeploymentStore(YesSql.ISession session)
        : base(session)
    {
    }
}
