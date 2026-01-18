namespace CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;

/// <summary>
/// Represents MongoDB-specific data source configuration metadata.
/// This metadata is stored on the AIDataSource and contains MongoDB connection settings.
/// Query-time parameters should use <see cref="Models.AzureRagChatMetadata"/> on the AIProfile instead.
/// </summary>
public sealed class AzureMongoDBDataSourceMetadata
{
    /// <summary>
    /// Gets or sets the MongoDB index name.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB endpoint name.
    /// </summary>
    public string EndpointName { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB application name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB collection name.
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the authentication configuration for MongoDB.
    /// </summary>
    public AzureAIProfileMongoDBAuthenticationType Authentication { get; set; }
}
