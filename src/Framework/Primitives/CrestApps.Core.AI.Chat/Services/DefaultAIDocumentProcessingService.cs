using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Chat.Services;

/// <summary>
/// Default host-agnostic document processor used by MVC and Orchard Core hosts.
/// </summary>
public sealed class DefaultAIDocumentProcessingService : IAIDocumentProcessingService
{
    private const int MaxEmbeddingTotalChars = 25000;

    private readonly IServiceProvider _serviceProvider;
    private readonly IAITextNormalizer _textNormalizer;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DefaultAIDocumentProcessingService> _logger;

    public DefaultAIDocumentProcessingService(
        IServiceProvider serviceProvider,
        IAITextNormalizer textNormalizer,
        IOptions<ChatDocumentsOptions> extractorOptions,
        TimeProvider timeProvider,
        ILogger<DefaultAIDocumentProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _textNormalizer = textNormalizer;
        _extractorOptions = extractorOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DocumentProcessingResult> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(referenceId);
        ArgumentException.ThrowIfNullOrEmpty(referenceType);

        var extension = Path.GetExtension(file.FileName);
        var options = _extractorOptions.Value;

        var reader = _serviceProvider.GetKeyedService<IngestionDocumentReader>(extension);
        if (reader == null)
        {
            return DocumentProcessingResult.Failed($"No document reader registered for extension '{extension}'.");
        }

        string text;
        try
        {
            using var stream = file.OpenReadStream();
            var mediaType = MediaTypeHelper.InferMediaType(extension, file.ContentType);
            var ingestionDoc = await reader.ReadAsync(stream, file.FileName, mediaType);

            text = string.Join('\n', ingestionDoc.EnumerateContent()
                .Select(element => element.Text)
                .Where(content => !string.IsNullOrWhiteSpace(content)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document processing: failed to read file '{FileName}' with extension '{Extension}'.", file.FileName, extension);

            return DocumentProcessingResult.Failed($"Failed to read the document '{file.FileName}'.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document processing: no text content extracted from file '{FileName}'.", file.FileName);
            }

            return DocumentProcessingResult.Failed("Could not extract text content from the document.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Document processing: extracted {TextLength} chars from '{FileName}' (extension: {Extension}).",
                text.Length,
                file.FileName,
                extension);
        }

        text = await _textNormalizer.NormalizeContentAsync(text);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var document = new AIDocument
        {
            ItemId = UniqueId.GenerateId(),
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedUtc = now,
        };

        var chunks = new List<AIDocumentChunk>();

        if (ShouldGenerateEmbeddings(extension, text.Length, embeddingGenerator, options))
        {
            var textChunks = await _textNormalizer.NormalizeAndChunkAsync(text);
            textChunks = LimitChunksForEmbedding(textChunks);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document processing: generated {ChunkCount} chunk(s) for '{FileName}'.", textChunks.Count, file.FileName);
            }

            GeneratedEmbeddings<Embedding<float>> embeddings = null;

            if (embeddingGenerator != null && textChunks.Count > 0)
            {
                try
                {
                    embeddings = await embeddingGenerator.GenerateAsync(textChunks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate embeddings for '{FileName}'. Chunks will be stored without embeddings.", file.FileName);
                }
            }

            for (var i = 0; i < textChunks.Count; i++)
            {
                chunks.Add(new AIDocumentChunk
                {
                    ItemId = UniqueId.GenerateId(),
                    AIDocumentId = document.ItemId,
                    ReferenceId = referenceId,
                    ReferenceType = referenceType,
                    Content = textChunks[i],
                    Embedding = embeddings != null && i < embeddings.Count ? embeddings[i].Vector.ToArray() : null,
                    Index = i,
                });
            }
        }
        else if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Document processing: skipping embedding generation for '{FileName}' (extension={Extension}, textLength={TextLength}, hasGenerator={HasGenerator}).",
                file.FileName,
                extension,
                text.Length,
                embeddingGenerator != null);
        }

        var documentInfo = new ChatDocumentInfo
        {
            DocumentId = document.ItemId,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
        };

        return DocumentProcessingResult.Succeeded(document, documentInfo, chunks);
    }

    private static bool ShouldGenerateEmbeddings(
        string extension,
        int textLength,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ChatDocumentsOptions options)
    {
        if (embeddingGenerator == null)
        {
            return false;
        }

        if (!options.EmbeddableFileExtensions.Contains(extension))
        {
            return false;
        }

        if (textLength > MaxEmbeddingTotalChars * 2)
        {
            return false;
        }

        return true;
    }

    private static List<string> LimitChunksForEmbedding(List<string> chunks)
    {
        var limitedChunks = new List<string>();
        var totalLength = 0;

        foreach (var chunk in chunks)
        {
            if (totalLength + chunk.Length > MaxEmbeddingTotalChars)
            {
                break;
            }

            limitedChunks.Add(chunk);
            totalLength += chunk.Length;
        }

        return limitedChunks;
    }
}
