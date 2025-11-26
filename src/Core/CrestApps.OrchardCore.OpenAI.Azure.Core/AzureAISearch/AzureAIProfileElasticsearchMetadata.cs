namespace CrestApps.OrchardCore.OpenAI.Azure.Core.AzureAISearch;

public sealed class AzureAIProfileElasticsearchMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }
}
