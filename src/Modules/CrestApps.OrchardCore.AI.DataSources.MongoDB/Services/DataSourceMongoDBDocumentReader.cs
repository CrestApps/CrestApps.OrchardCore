using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AI.DataSources.MongoDB.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;

namespace CrestApps.OrchardCore.AI.DataSources.MongoDB.Services;

/// <summary>
/// Reads documents from a MongoDB source collection.
/// </summary>
internal sealed class DataSourceMongoDBDocumentReader : IDataSourceDocumentReader
{
    private const int BatchSize = 1000;

    private readonly IIndexProfileStore _indexProfileStore;
    private readonly ILogger<DataSourceMongoDBDocumentReader> _logger;

    public DataSourceMongoDBDocumentReader(
        IIndexProfileStore indexProfileStore,
        ILogger<DataSourceMongoDBDocumentReader> logger)
    {
        _indexProfileStore = indexProfileStore;
        _logger = logger;
    }

    public async IAsyncEnumerable<KeyValuePair<string, SourceDocument>> ReadAsync(
        IndexProfile indexProfile,
        string keyFieldName,
        string titleFieldName,
        string contentFieldName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (indexProfile == null)
        {
            yield break;
        }

        var metadata = indexProfile.As<MongoDBDataSourceConnectionMetadata>();

        if (string.IsNullOrEmpty(metadata?.ConnectionString) ||
            string.IsNullOrEmpty(metadata.DatabaseName) ||
            string.IsNullOrEmpty(metadata.CollectionName))
        {
            _logger.LogWarning("MongoDB connection metadata is incomplete for index '{IndexName}'.", indexProfile.IndexFullName);
            yield break;
        }

        IMongoCollection<BsonDocument> collection;

        try
        {
            var client = new MongoClient(metadata.ConnectionString);
            var database = client.GetDatabase(metadata.DatabaseName);
            collection = database.GetCollection<BsonDocument>(metadata.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MongoDB for index '{IndexName}'.", indexProfile.IndexFullName);
            yield break;
        }

        var filter = Builders<BsonDocument>.Filter.Empty;
        var options = new FindOptions<BsonDocument>
        {
            BatchSize = BatchSize,
        };

        using var cursor = await collection.FindAsync(filter, options, cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                var key = !string.IsNullOrEmpty(keyFieldName) && doc.TryGetValue(keyFieldName, out var keyValue) && !keyValue.IsBsonNull
                    ? keyValue.ToString()
                    : doc.TryGetValue("_id", out var idValue)
                        ? idValue.ToString()
                        : null;

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                yield return new KeyValuePair<string, SourceDocument>(
                    key, ExtractDocument(doc, titleFieldName, contentFieldName));
            }
        }
    }

    private static SourceDocument ExtractDocument(BsonDocument doc, string titleFieldName, string contentFieldName)
    {
        string title = null;
        string content = null;

        if (!string.IsNullOrEmpty(titleFieldName) && doc.TryGetValue(titleFieldName, out var titleValue))
        {
            title = titleValue.IsBsonNull ? null : titleValue.ToString();
        }

        if (!string.IsNullOrEmpty(contentFieldName) && doc.TryGetValue(contentFieldName, out var contentValue))
        {
            content = contentValue.IsBsonNull ? null : contentValue.ToString();
        }
        else
        {
            content = doc.ToJson();
        }

        if (string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(content))
        {
            title = ExtractTitleFromContent(content);
        }

        // Populate all source fields for filter field propagation.
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc)
        {
            if (element.Name == "_id")
            {
                continue;
            }

            fields[element.Name] = BsonValueToObject(element.Value);
        }

        return new SourceDocument
        {
            Title = title,
            Content = content,
            Fields = fields,
        };
    }

    private static string ExtractTitleFromContent(string content)
    {
        var firstLine = content.AsSpan();
        var newlineIndex = firstLine.IndexOfAny('\r', '\n');

        if (newlineIndex > 0)
        {
            firstLine = firstLine[..newlineIndex];
        }

        if (firstLine.Length > 200)
        {
            firstLine = firstLine[..200];
        }

        return firstLine.ToString().Trim();
    }

    private static object BsonValueToObject(BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.Double => value.AsDouble,
            BsonType.Boolean => value.AsBoolean,
            BsonType.DateTime => value.ToUniversalTime(),
            BsonType.Decimal128 => value.AsDecimal,
            _ => value.ToString(),
        };
    }
}
