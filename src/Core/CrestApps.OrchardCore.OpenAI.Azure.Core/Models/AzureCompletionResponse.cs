namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionResponse
{
    public string Id { get; set; }

    public string Model { get; set; }

    public string Object { get; set; }

    public AzureCompletionChoice[] Choices { get; set; }

    public AzureCompletionUsage Usage { get; set; }
}
