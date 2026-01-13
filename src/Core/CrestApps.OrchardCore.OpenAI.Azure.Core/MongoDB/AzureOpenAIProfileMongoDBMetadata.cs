namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;

public sealed class AzureOpenAIProfileMongoDBMetadata
{
    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }
}
