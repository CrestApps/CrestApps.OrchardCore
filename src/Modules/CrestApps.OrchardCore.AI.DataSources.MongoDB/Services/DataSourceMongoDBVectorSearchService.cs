using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.MongoDB.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OrchardCore.Entities;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.MongoDB.Services;

/// <summary>
/// MongoDB Atlas implementation of <see cref="IDataSourceVectorSearchService"/>
/// for searching data source embedding indexes using MongoDB Atlas Vector Search ($vectorSearch).
/// </summary>
internal sealed class DataSourceMongoDBVectorSearchService : IDataSourceVectorSearchService
{
    private readonly ILogger _logger;

    public DataSourceMongoDBVectorSearchService(ILogger<DataSourceMongoDBVectorSearchService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<DataSourceSearchResult>> SearchAsync(
        IndexProfile indexProfile,
        float[] embedding,
        string dataSourceId,
        int topN,
        string filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexProfile);
        ArgumentNullException.ThrowIfNull(embedding);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSourceId);

        if (embedding.Length == 0)
        {
            return [];
        }

        try
        {
            var metadata = indexProfile.As<MongoDBDataSourceConnectionMetadata>();

            if (string.IsNullOrEmpty(metadata?.ConnectionString) ||
                string.IsNullOrEmpty(metadata.DatabaseName) ||
                string.IsNullOrEmpty(metadata.CollectionName))
            {
                _logger.LogWarning("MongoDB connection metadata is incomplete for index '{IndexName}'.", indexProfile.IndexFullName);
                return [];
            }

            var client = new MongoClient(metadata.ConnectionString);
            var database = client.GetDatabase(metadata.DatabaseName);
            var collection = database.GetCollection<BsonDocument>(metadata.CollectionName);

            var vectorSearchIndexName = metadata.VectorSearchIndexName ?? "default";

            // Build the $vectorSearch aggregation stage.
            var vectorSearchStage = new BsonDocument("$vectorSearch", new BsonDocument
            {
                { "index", vectorSearchIndexName },
                { "path", DataSourceConstants.ColumnNames.Embedding },
                { "queryVector", new BsonArray(embedding.Select(v => (double)v)) },
                { "numCandidates", topN * 10 },
                { "limit", topN },
                { "filter", BuildFilter(dataSourceId, filter) },
            });

            // Project the fields we need and include the vector search score.
            var projectStage = new BsonDocument("$project", new BsonDocument
            {
                { DataSourceConstants.ColumnNames.ChunkId, 1 },
                { DataSourceConstants.ColumnNames.ReferenceId, 1 },
                { DataSourceConstants.ColumnNames.DataSourceId, 1 },
                { DataSourceConstants.ColumnNames.Title, 1 },
                { DataSourceConstants.ColumnNames.Content, 1 },
                { DataSourceConstants.ColumnNames.ChunkIndex, 1 },
                { "score", new BsonDocument("$meta", "vectorSearchScore") },
            });

            var pipeline = new[] { vectorSearchStage, projectStage };

            var results = new List<DataSourceSearchResult>();

            using var cursor = await collection.AggregateAsync<BsonDocument>(
                pipeline, cancellationToken: cancellationToken);

            while (await cursor.MoveNextAsync(cancellationToken))
            {
                foreach (var doc in cursor.Current)
                {
                    var result = ExtractResult(doc);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .Take(topN)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing MongoDB Atlas vector search for index '{IndexName}'.",
                indexProfile.IndexFullName);
            return [];
        }
    }

    private static BsonDocument BuildFilter(string dataSourceId, string additionalFilter)
    {
        var filters = new BsonArray
        {
            new BsonDocument("equals", new BsonDocument
            {
                { "path", DataSourceConstants.ColumnNames.DataSourceId },
                { "value", dataSourceId },
            }),
        };

        // If additional filter is provided (already translated from OData to MongoDB BSON),
        // parse and add it.
        if (!string.IsNullOrWhiteSpace(additionalFilter))
        {
            try
            {
                var parsedFilter = BsonDocument.Parse(additionalFilter);
                filters.Add(parsedFilter);
            }
            catch
            {
                // Ignore invalid filter — don't break the search.
            }
        }

        if (filters.Count == 1)
        {
            return filters[0].AsBsonDocument;
        }

        return new BsonDocument("compound", new BsonDocument
        {
            { "must", filters },
        });
    }

    private static DataSourceSearchResult ExtractResult(BsonDocument doc)
    {
        var referenceId = doc.TryGetValue(DataSourceConstants.ColumnNames.ReferenceId, out var refValue) && !refValue.IsBsonNull
            ? refValue.AsString
            : null;

        var title = doc.TryGetValue(DataSourceConstants.ColumnNames.Title, out var titleValue) && !titleValue.IsBsonNull
            ? titleValue.AsString
            : null;

        var content = doc.TryGetValue(DataSourceConstants.ColumnNames.Content, out var contentValue) && !contentValue.IsBsonNull
            ? contentValue.AsString
            : null;

        var chunkIndex = doc.TryGetValue(DataSourceConstants.ColumnNames.ChunkIndex, out var chunkIndexValue) && !chunkIndexValue.IsBsonNull
            ? chunkIndexValue.ToInt32()
            : 0;

        var score = doc.TryGetValue("score", out var scoreValue) && !scoreValue.IsBsonNull
            ? (float)scoreValue.ToDouble()
            : 0f;

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        return new DataSourceSearchResult
        {
            ReferenceId = referenceId,
            Title = title,
            Content = content,
            ChunkIndex = chunkIndex,
            Score = score,
        };
    }
}
