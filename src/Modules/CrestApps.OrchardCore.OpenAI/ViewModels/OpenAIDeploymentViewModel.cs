using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public class OpenAIDeploymentViewModel
{
    public string DisplayName { get; set; }

    public IShape Editor { get; set; }
}
