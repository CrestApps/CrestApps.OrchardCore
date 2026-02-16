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
public sealed class DataSourceMongoDBVectorSearchService : IDataSourceVectorSearchService
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
        IEnumerable<string> referenceIds = null,
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
                { "path", DataSourceConstants.ColumnNames.ChunksEmbedding },
                { "queryVector", new BsonArray(embedding.Select(v => (double)v)) },
                { "numCandidates", topN * 10 },
                { "limit", topN },
                { "filter", BuildFilter(dataSourceId, referenceIds) },
            });

            // Project the fields we need and include the vector search score.
            var projectStage = new BsonDocument("$project", new BsonDocument
            {
                { DataSourceConstants.ColumnNames.ReferenceId, 1 },
                { DataSourceConstants.ColumnNames.DataSourceId, 1 },
                { DataSourceConstants.ColumnNames.Title, 1 },
                { DataSourceConstants.ColumnNames.Chunks, 1 },
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
                    var docResults = ExtractResults(doc);
                    results.AddRange(docResults);
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

    private static BsonDocument BuildFilter(string dataSourceId, IEnumerable<string> referenceIds)
    {
        var filters = new BsonArray
        {
            new BsonDocument("equals", new BsonDocument
            {
                { "path", DataSourceConstants.ColumnNames.DataSourceId },
                { "value", dataSourceId },
            }),
        };

        var referenceIdList = referenceIds?.ToList();
        if (referenceIdList is { Count: > 0 })
        {
            filters.Add(new BsonDocument("in", new BsonDocument
            {
                { "path", DataSourceConstants.ColumnNames.ReferenceId },
                { "value", new BsonArray(referenceIdList) },
            }));
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

    private static List<DataSourceSearchResult> ExtractResults(BsonDocument doc)
    {
        var results = new List<DataSourceSearchResult>();

        var referenceId = doc.TryGetValue(DataSourceConstants.ColumnNames.ReferenceId, out var refValue) && !refValue.IsBsonNull
            ? refValue.AsString
            : null;

        var title = doc.TryGetValue(DataSourceConstants.ColumnNames.Title, out var titleValue) && !titleValue.IsBsonNull
            ? titleValue.AsString
            : null;

        var score = doc.TryGetValue("score", out var scoreValue) && !scoreValue.IsBsonNull
            ? (float)scoreValue.ToDouble()
            : 0f;

        if (!doc.TryGetValue(DataSourceConstants.ColumnNames.Chunks, out var chunksValue) || !chunksValue.IsBsonArray)
        {
            return results;
        }

        foreach (var chunk in chunksValue.AsBsonArray)
        {
            if (chunk is not BsonDocument chunkDoc)
            {
                continue;
            }

            var chunkText = chunkDoc.TryGetValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Text, out var textValue) && !textValue.IsBsonNull
                ? textValue.AsString
                : null;

            var chunkIndex = chunkDoc.TryGetValue(DataSourceConstants.ColumnNames.ChunksColumnNames.Index, out var indexValue) && !indexValue.IsBsonNull
                ? indexValue.ToInt32()
                : 0;

            if (!string.IsNullOrEmpty(chunkText))
            {
                results.Add(new DataSourceSearchResult
                {
                    ReferenceId = referenceId,
                    Title = title,
                    Text = chunkText,
                    ChunkIndex = chunkIndex,
                    Score = score,
                });
            }
        }

        return results;
    }
}
