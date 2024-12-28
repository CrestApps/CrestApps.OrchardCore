using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class ModelDeploymentEntry
{
    public ModelDeployment Deployment { get; set; }

    public IShape Shape { get; set; }
}
