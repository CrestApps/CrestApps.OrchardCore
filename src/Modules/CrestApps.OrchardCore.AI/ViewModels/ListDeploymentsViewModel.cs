using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class ListDeploymentsViewModel
{
    public IList<AIDeploymentEntry> Deployments { get; set; }

    public AIDeploymentOptions Options { get; set; }

    public IEnumerable<string> SourceNames { get; set; }

    public IShape Pager { get; set; }
}
