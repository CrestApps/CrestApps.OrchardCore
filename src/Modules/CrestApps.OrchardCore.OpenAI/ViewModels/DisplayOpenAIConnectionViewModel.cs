namespace CrestApps.OrchardCore.OpenAI.ViewModels;

internal sealed class DisplayOpenAIConnectionViewModel
{
    public string Id { get; set; }

    public string DisplayText { get; set; }

    public string Name { get; set; }

    public string DefaultDeploymentName { get; set; }

    public Uri Endpoint { get; set; }
}
