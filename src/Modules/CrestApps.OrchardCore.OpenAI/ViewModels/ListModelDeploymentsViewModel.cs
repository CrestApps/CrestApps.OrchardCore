using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class ListModelDeploymentsViewModel
{
    public IList<ModelDeploymentEntry> Deployments { get; set; }

    public ModelDeploymentOptions Options { get; set; }

    public IEnumerable<string> SourceNames { get; set; }

    public IShape Pager { get; set; }
}
