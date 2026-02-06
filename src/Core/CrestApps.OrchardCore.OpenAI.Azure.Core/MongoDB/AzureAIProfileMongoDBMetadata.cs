using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;

/// <summary>
/// Contains metadata for MongoDB data sources.
/// </summary>
/// <remarks>
/// This class is obsolete. Use <see cref="AzureAIDataSourceIndexMetadata"/> for index configuration
/// on the AIDataSource, <see cref="AzureMongoDBDataSourceMetadata"/> for MongoDB-specific connection settings,
/// and <see cref="AzureRagChatMetadata"/> for query-time parameters on the AIProfile.
/// </remarks>
[Obsolete($"Use {nameof(AzureMongoDBDataSourceMetadata)} for MongoDB-specific settings, and {nameof(AzureRagChatMetadata)} for query-time parameters.")]
public sealed class AzureAIProfileMongoDBMetadata
{
    public string IndexName { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public AzureAIProfileMongoDBAuthenticationType Authentication { get; set; }

    public string EndpointName { get; set; }

    public string AppName { get; set; }

    public string CollectionName { get; set; }

    public string DatabaseName { get; set; }
}
