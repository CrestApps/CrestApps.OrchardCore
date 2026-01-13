namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

public sealed class AzureOpenAIProfileElasticsearchMetadata
{
    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
