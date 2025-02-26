namespace CrestApps.OrchardCore.OpenAI.Core.Models;

public sealed class OpenAIConnection
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public Uri Endpoint { get; set; }

    public string ApiKey { get; set; }

    public string DefaultDeploymentName { get; set; }
}
