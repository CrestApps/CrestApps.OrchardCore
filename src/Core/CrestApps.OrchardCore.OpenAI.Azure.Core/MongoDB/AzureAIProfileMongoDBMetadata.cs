namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;

public sealed class AzureAIProfileMongoDBMetadata
{
    public string IndexName { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileMongoDBMetadata")]
    public int? Strictness { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileMongoDBMetadata")]
    public int? TopNDocuments { get; set; }

    [Obsolete("This should not be used, instead we should use AzureOpenAIProfileMongoDBMetadata")]
    public string Filter { get; set; }

    public AzureAIProfileMongoDBAuthenticationType Authentication { get; set; }

    public string EndpointName { get; set; }

    public string AppName { get; set; }

    public string CollectionName { get; set; }

    public string DatabaseName { get; set; }
}
