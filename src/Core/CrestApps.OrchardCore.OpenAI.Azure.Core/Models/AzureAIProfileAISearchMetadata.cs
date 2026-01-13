namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureAIProfileAISearchMetadata
{
    public string IndexName { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileAISearchMetadata")]
    public int? Strictness { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileAISearchMetadata")]
    public int? TopNDocuments { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileAISearchMetadata")]
    public string Filter { get; set; }
}
