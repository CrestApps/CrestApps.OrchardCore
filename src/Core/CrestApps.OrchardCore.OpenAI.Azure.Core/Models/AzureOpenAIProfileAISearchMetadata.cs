namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureOpenAIProfileAISearchMetadata
{
    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
