namespace CrestApps.OrchardCore.AI.DataSources.MongoDB.Models;

/// <summary>
/// MongoDB-specific connection metadata stored on the knowledge base IndexProfile.
/// Contains the connection settings required to connect to a MongoDB Atlas instance.
/// </summary>
internal sealed class MongoDBDataSourceConnectionMetadata
{
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the MongoDB collection name that serves as the knowledge base index.
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// Gets or sets the name of the MongoDB Atlas Vector Search index.
    /// </summary>
    public string VectorSearchIndexName { get; set; }
}
