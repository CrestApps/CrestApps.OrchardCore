using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class AIDeploymentEntry
{
    public AIDeployment Deployment { get; set; }

    public IShape Shape { get; set; }
}
