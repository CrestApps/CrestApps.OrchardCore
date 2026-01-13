namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

public sealed class AzureAIProfileElasticsearchMetadata
{
    public string IndexName { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileElasticsearchMetadata")]
    public int? Strictness { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileElasticsearchMetadata")]
    public int? TopNDocuments { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileElasticsearchMetadata")]
    public string Filter { get; set; }
}
