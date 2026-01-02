using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Elasticsearch;

/// <summary>
/// Elasticsearch implementation of IDocumentIndexHandler.
/// Stores document embeddings in Elasticsearch for vector similarity search.
/// </summary>
public sealed class ElasticsearchDocumentIndexHandler : IDocumentIndexHandler
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ElasticsearchDocumentIndexHandler> _logger;

    private const string IndexName = "chat-interaction-documents";
    private ElasticsearchClient _client;
    private bool _initialized;

    public ElasticsearchDocumentIndexHandler(
        IConfiguration configuration,
        ILogger<ElasticsearchDocumentIndexHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeIndexAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            var client = GetClient();
            if (client == null)
            {
                _logger.LogWarning("Elasticsearch client is not configured. Document indexing will be skipped.");
                return;
            }

            // Check if index exists
            var existsResponse = await client.Indices.ExistsAsync(IndexName, cancellationToken);

            if (!existsResponse.Exists)
            {
                // Create index with mappings for vector search
                var createResponse = await client.Indices.CreateAsync(IndexName, c => c
                    .Mappings(m => m
                        .Properties(new Properties
                        {
                            { "chunkId", new KeywordProperty() },
                            { "documentId", new KeywordProperty() },
                            { "sessionId", new KeywordProperty() },
                            { "content", new TextProperty() },
                            { "embedding", new DenseVectorProperty
                            {
                                Dims = 1536, // Default for text-embedding-ada-002
                                Index = true,
                                Similarity = DenseVectorSimilarity.Cosine
                            }},
                            { "chunkIndex", new IntegerNumberProperty() },
                            { "fileName", new KeywordProperty() },
                            { "indexedUtc", new DateProperty() }
                        })
                    ), cancellationToken);

                if (!createResponse.IsValidResponse)
                {
                    _logger.LogError("Failed to create Elasticsearch index: {Error}", createResponse.DebugInformation);
                }
                else
                {
                    _logger.LogInformation("Created Elasticsearch index: {IndexName}", IndexName);
                }
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Elasticsearch index");
        }
    }

    public async Task IndexDocumentAsync(DocumentIndexContext context, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return;
        }

        await InitializeIndexAsync(cancellationToken);

        try
        {
            var documents = context.Chunks.Select(chunk => new Dictionary<string, object>
            {
                ["chunkId"] = chunk.ChunkId,
                ["documentId"] = chunk.DocumentId,
                ["sessionId"] = chunk.SessionId,
                ["content"] = chunk.Content,
                ["embedding"] = chunk.Embedding,
                ["chunkIndex"] = chunk.ChunkIndex,
                ["fileName"] = chunk.FileName,
                ["indexedUtc"] = chunk.IndexedUtc
            }).ToList();

            var bulkResponse = await client.BulkAsync(b => b
                .Index(IndexName)
                .IndexMany(documents, (bd, doc) => bd.Id((string)doc["chunkId"])),
                cancellationToken);

            if (!bulkResponse.IsValidResponse)
            {
                _logger.LogError("Failed to index document chunks: {Error}", bulkResponse.DebugInformation);
            }
            else
            {
                _logger.LogInformation("Indexed {Count} chunks for document {DocumentId}", context.Chunks.Count, context.DocumentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document {DocumentId}", context.DocumentId);
        }
    }

    public async Task RemoveDocumentAsync(string sessionId, string documentId, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return;
        }

        try
        {
            var deleteResponse = await client.DeleteByQueryAsync<object>(IndexName, d => d
                .Query(q => q
                    .Bool(b => b
                        .Must(
                            m => m.Term(t => t.Field("sessionId").Value(sessionId)),
                            m => m.Term(t => t.Field("documentId").Value(documentId))
                        )
                    )
                ), cancellationToken);

            if (!deleteResponse.IsValidResponse)
            {
                _logger.LogError("Failed to remove document chunks: {Error}", deleteResponse.DebugInformation);
            }
            else
            {
                _logger.LogInformation("Removed chunks for document {DocumentId} in session {SessionId}", documentId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document {DocumentId}", documentId);
        }
    }

    public async Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return;
        }

        try
        {
            var deleteResponse = await client.DeleteByQueryAsync<object>(IndexName, d => d
                .Query(q => q
                    .Term(t => t.Field("sessionId").Value(sessionId))
                ), cancellationToken);

            if (!deleteResponse.IsValidResponse)
            {
                _logger.LogError("Failed to remove session chunks: {Error}", deleteResponse.DebugInformation);
            }
            else
            {
                _logger.LogInformation("Removed all chunks for session {SessionId}", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing session {SessionId}", sessionId);
        }
    }

    public async Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        string sessionId,
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return [];
        }

        try
        {
            var searchResponse = await client.SearchAsync<Dictionary<string, object>>(s => s
                .Indices(IndexName)
                .Size(topK)
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m.Term(t => t.Field("sessionId").Value(sessionId)))
                        .Should(sh => sh
                            .ScriptScore(ss => ss
                                .Query(sq => sq.MatchAll())
                                .Script(sc => sc
                                    .Source("cosineSimilarity(params.query_vector, 'embedding') + 1.0")
                                    .Params(p => p.Add("query_vector", queryEmbedding))
                                )
                            )
                        )
                    )
                ), cancellationToken);

            if (!searchResponse.IsValidResponse)
            {
                _logger.LogError("Failed to search documents: {Error}", searchResponse.DebugInformation);
                return [];
            }

            return searchResponse.Hits.Select(hit =>
            {
                var source = hit.Source;
                return new DocumentChunkSearchResult
                {
                    Chunk = new DocumentChunk
                    {
                        ChunkId = source["chunkId"]?.ToString(),
                        DocumentId = source["documentId"]?.ToString(),
                        SessionId = source["sessionId"]?.ToString(),
                        Content = source["content"]?.ToString(),
                        ChunkIndex = source.TryGetValue("chunkIndex", out var idx) && int.TryParse(idx?.ToString(), out var i) ? i : 0,
                        FileName = source["fileName"]?.ToString(),
                        IndexedUtc = source.TryGetValue("indexedUtc", out var dt) && DateTime.TryParse(dt?.ToString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d.ToUniversalTime() : DateTime.UtcNow
                    },
                    Score = (float)(hit.Score ?? 0)
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents in session {SessionId}", sessionId);
            return [];
        }
    }

    private ElasticsearchClient GetClient()
    {
        if (_client != null)
        {
            return _client;
        }

        var connectionString = _configuration.GetConnectionString("Elasticsearch");
        if (string.IsNullOrEmpty(connectionString))
        {
            return null;
        }

        try
        {
            var settings = new ElasticsearchClientSettings(new Uri(connectionString));
            _client = new ElasticsearchClient(settings);
            return _client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Elasticsearch client");
            return null;
        }
    }
}
