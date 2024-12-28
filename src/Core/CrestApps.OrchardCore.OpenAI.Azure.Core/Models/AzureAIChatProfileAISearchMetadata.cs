namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureAIChatProfileAISearchMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }
}
