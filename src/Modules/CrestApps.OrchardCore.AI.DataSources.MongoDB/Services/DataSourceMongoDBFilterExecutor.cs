using CrestApps.OrchardCore.AI.DataSources.MongoDB.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using OrchardCore.Entities;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.DataSources.MongoDB.Services;

/// <summary>
/// Executes MongoDB filter queries against a source collection
/// and returns matching document keys for two-phase RAG search.
/// </summary>
internal sealed class DataSourceMongoDBFilterExecutor : IDataSourceFilterExecutor
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ILogger<DataSourceMongoDBFilterExecutor> _logger;

    public DataSourceMongoDBFilterExecutor(
        IIndexProfileStore indexProfileStore,
        ILogger<DataSourceMongoDBFilterExecutor> logger)
    {
        _indexProfileStore = indexProfileStore;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> ExecuteAsync(
        string indexName,
        string filter,
        CancellationToken cancellationToken = default)
    {
        // Validate the filter is valid JSON/BSON.
        BsonDocument filterDoc;

        try
        {
            filterDoc = BsonSerializer.Deserialize<BsonDocument>(filter);
        }
        catch (Exception)
        {
            _logger.LogWarning("Invalid MongoDB filter JSON provided.");
            return null;
        }

        try
        {
            var indexProfile = await _indexProfileStore.FindByNameAsync(indexName);

            if (indexProfile == null)
            {
                _logger.LogWarning("Index profile '{IndexName}' not found.", indexName);
                return null;
            }

            var metadata = indexProfile.As<MongoDBDataSourceConnectionMetadata>();

            if (string.IsNullOrEmpty(metadata?.ConnectionString) ||
                string.IsNullOrEmpty(metadata.DatabaseName) ||
                string.IsNullOrEmpty(metadata.CollectionName))
            {
                _logger.LogWarning("MongoDB connection metadata is incomplete for index '{IndexName}'.", indexName);
                return null;
            }

            var client = new MongoClient(metadata.ConnectionString);
            var database = client.GetDatabase(metadata.DatabaseName);
            var collection = database.GetCollection<BsonDocument>(metadata.CollectionName);

            var projection = Builders<BsonDocument>.Projection.Include("_id");
            var options = new FindOptions<BsonDocument, BsonDocument>
            {
                Projection = projection,
                Limit = 10000,
            };

            var keys = new List<string>();

            using var cursor = await collection.FindAsync(
                new BsonDocumentFilterDefinition<BsonDocument>(filterDoc),
                options,
                cancellationToken);

            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var doc in cursor.Current)
                {
                    if (doc.TryGetValue("_id", out var idValue))
                    {
                        var keyStr = idValue.ToString();

                        if (!string.IsNullOrEmpty(keyStr))
                        {
                            keys.Add(keyStr);
                        }
                    }
                }
            }

            return keys.Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing MongoDB filter query against index '{IndexName}'.", indexName);
            return null;
        }
    }
}
