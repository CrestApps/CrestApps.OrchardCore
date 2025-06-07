namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureAIProfileMongoDBMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public AzureAIProfileMongoDBAuthenticationType Authentication { get; set; }

    public string EndpointName { get; set; }

    public string AppName { get; set; }

    public string CollectionName { get; set; }
}
