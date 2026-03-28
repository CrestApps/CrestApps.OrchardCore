using CrestApps.AI.Models;
using CrestApps.Data.YesSql;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class AIDeploymentIndex : CatalogItemIndex, INameAwareIndex, ISourceAwareIndex
{
    public string Name { get; set; }
    public string Source { get; set; }
}

public sealed class AIDeploymentIndexProvider : IndexProvider<AIDeployment>
{
    public override void Describe(DescribeContext<AIDeployment> context)
    {
        context.For<AIDeploymentIndex>()
            .Map(deployment => new AIDeploymentIndex
            {
                ItemId = deployment.ItemId,
                Name = deployment.Name,
                Source = deployment.Source,
            });
    }
}
