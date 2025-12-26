using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// Default implementation of IDocumentEmbeddingService.
/// Handles document chunking, embedding generation, and delegates storage to IDocumentIndexHandler implementations.
/// </summary>
public sealed class DefaultDocumentEmbeddingService : IDocumentEmbeddingService
{
    private readonly IAIClientFactory _clientFactory;
    private readonly IEnumerable<IDocumentIndexHandler> _indexHandlers;
    private readonly ILogger<DefaultDocumentEmbeddingService> _logger;

    // Target chunk size in characters (approximately 500 tokens)
    private const int ChunkSize = 2000;
    private const int ChunkOverlap = 200;

    public DefaultDocumentEmbeddingService(
        IAIClientFactory clientFactory,
        IEnumerable<IDocumentIndexHandler> indexHandlers,
        ILogger<DefaultDocumentEmbeddingService> logger)
    {
        _clientFactory = clientFactory;
        _indexHandlers = indexHandlers.OrderBy(h => h.Priority);
        _logger = logger;
    }

    public async Task IndexDocumentAsync(
        string sessionId,
        string documentId,
        string fileName,
        string content,
        string providerName,
        string connectionName,
        string deploymentName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("Empty content provided for document {DocumentId} in session {SessionId}", documentId, sessionId);
            return;
        }

        // Split content into chunks
        var textChunks = ChunkText(content);
        if (textChunks.Count == 0)
        {
            _logger.LogWarning("No chunks generated for document {DocumentId}", documentId);
            return;
        }

        _logger.LogInformation("Generated {ChunkCount} chunks for document {DocumentId}", textChunks.Count, documentId);

        // Generate embeddings for all chunks
        var embeddingGenerator = await _clientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);
        if (embeddingGenerator == null)
        {
            _logger.LogError("Failed to create embedding generator for provider {Provider}, connection {Connection}, deployment {Deployment}",
                providerName, connectionName, deploymentName);
            return;
        }

        var chunks = new List<DocumentChunk>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < textChunks.Count; i++)
        {
            var embedding = await embeddingGenerator.GenerateEmbeddingAsync(textChunks[i], cancellationToken: cancellationToken);
            
            chunks.Add(new DocumentChunk
            {
                ChunkId = $"{documentId}_{i}",
                DocumentId = documentId,
                SessionId = sessionId,
                Content = textChunks[i],
                Embedding = embedding.Vector.ToArray(),
                ChunkIndex = i,
                FileName = fileName,
                IndexedUtc = now
            });
        }

        var context = new DocumentIndexContext
        {
            SessionId = sessionId,
            DocumentId = documentId,
            FileName = fileName,
            Chunks = chunks
        };

        // Notify all handlers
        foreach (var handler in _indexHandlers)
        {
            try
            {
                await handler.IndexDocumentAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document {DocumentId} with handler {Handler}", documentId, handler.GetType().Name);
            }
        }
    }

    public async Task RemoveDocumentAsync(string sessionId, string documentId, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _indexHandlers)
        {
            try
            {
                await handler.RemoveDocumentAsync(sessionId, documentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing document {DocumentId} with handler {Handler}", documentId, handler.GetType().Name);
            }
        }
    }

    public async Task RemoveSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        foreach (var handler in _indexHandlers)
        {
            try
            {
                await handler.RemoveSessionAsync(sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing session {SessionId} with handler {Handler}", sessionId, handler.GetType().Name);
            }
        }
    }

    public async Task<IEnumerable<DocumentChunkSearchResult>> SearchAsync(
        string sessionId,
        string query,
        string providerName,
        string connectionName,
        string deploymentName,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var embeddingGenerator = await _clientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);
        if (embeddingGenerator == null)
        {
            _logger.LogError("Failed to create embedding generator for search");
            return [];
        }

        var queryEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
        
        var allResults = new List<DocumentChunkSearchResult>();

        foreach (var handler in _indexHandlers)
        {
            try
            {
                var results = await handler.SearchAsync(sessionId, queryEmbedding.Vector.ToArray(), topK, cancellationToken);
                allResults.AddRange(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching with handler {Handler}", handler.GetType().Name);
            }
        }

        // Return top K results across all handlers, sorted by score
        return allResults.OrderByDescending(r => r.Score).Take(topK);
    }

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        // First, try to split by paragraphs (double newlines)
        var paragraphs = Regex.Split(text, @"\n\s*\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        var currentChunk = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length + 2 <= ChunkSize)
            {
                if (currentChunk.Length > 0)
                {
                    currentChunk.Append("\n\n");
                }
                currentChunk.Append(paragraph);
            }
            else
            {
                // Current chunk is full, save it and start a new one
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    
                    // Start new chunk with overlap from previous
                    var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlap);
                    currentChunk.Clear();
                    if (!string.IsNullOrEmpty(overlapText))
                    {
                        currentChunk.Append(overlapText);
                        currentChunk.Append("\n\n");
                    }
                }

                // If the paragraph itself is larger than chunk size, split it
                if (paragraph.Length > ChunkSize)
                {
                    var subChunks = SplitLongParagraph(paragraph);
                    foreach (var subChunk in subChunks)
                    {
                        chunks.Add(subChunk);
                    }
                }
                else
                {
                    currentChunk.Append(paragraph);
                }
            }
        }

        // Don't forget the last chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
    }

    private static string GetOverlapText(string text, int overlapSize)
    {
        if (text.Length <= overlapSize)
        {
            return text;
        }

        var lastPart = text[^overlapSize..];
        // Try to start at a sentence boundary
        var sentenceStart = lastPart.IndexOf(". ");
        if (sentenceStart > 0 && sentenceStart < overlapSize / 2)
        {
            return lastPart[(sentenceStart + 2)..].Trim();
        }

        return lastPart.Trim();
    }

    private static List<string> SplitLongParagraph(string paragraph)
    {
        var chunks = new List<string>();
        
        // Split by sentences
        var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var currentChunk = new System.Text.StringBuilder();

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length + 1 <= ChunkSize)
            {
                if (currentChunk.Length > 0)
                {
                    currentChunk.Append(' ');
                }
                currentChunk.Append(sentence);
            }
            else
            {
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }
                
                // If a single sentence is too long, just add it as is
                if (sentence.Length > ChunkSize)
                {
                    chunks.Add(sentence[..ChunkSize]);
                    var remaining = sentence[ChunkSize..];
                    if (!string.IsNullOrWhiteSpace(remaining))
                    {
                        currentChunk.Append(remaining);
                    }
                }
                else
                {
                    currentChunk.Append(sentence);
                }
            }
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        return chunks;
    }
}
