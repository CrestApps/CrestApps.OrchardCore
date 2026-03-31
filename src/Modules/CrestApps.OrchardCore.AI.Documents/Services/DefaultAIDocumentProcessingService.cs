using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DataIngestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class DefaultAIDocumentProcessingService : IAIDocumentProcessingService
{
    private const int MaxEmbeddingTotalChars = 25000;

    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IOptions<AIProviderOptions> _providerOptions;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultAIDocumentProcessingService(
        IServiceProvider serviceProvider,
        IOptions<ChatDocumentsOptions> extractorOptions,
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IAIDeploymentManager deploymentManager,
        IClock clock,
        ILogger<DefaultAIDocumentProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _extractorOptions = extractorOptions;
        _aiClientFactory = aiClientFactory;
        _providerOptions = providerOptions;
        _deploymentManager = deploymentManager;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName)
    {
        var embeddingDeployment = await _deploymentManager.ResolveOrDefaultAsync(
            AIDeploymentType.Embedding,
            clientName: providerName,
            connectionName: connectionName);

        if (embeddingDeployment != null)
        {
            var generator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(
                embeddingDeployment.ClientName,
                embeddingDeployment.ConnectionName,
                embeddingDeployment.Name);

            if (generator == null)
            {
                _logger.LogWarning("Failed to create embedding generator for client {Client}, connection {Connection}, deployment {Deployment}. Documents will be stored without embeddings.",
                    embeddingDeployment.ClientName, embeddingDeployment.ConnectionName, embeddingDeployment.Name);
            }

            return generator;
        }

        // Fall back to legacy provider options lookup for backward compatibility.
        string deploymentName = null;

#pragma warning disable CS0618 // Type or member is obsolete
        if (!string.IsNullOrEmpty(providerName) &&
            !string.IsNullOrEmpty(connectionName) &&
            _providerOptions.Value.Providers.TryGetValue(providerName, out var provider))
        {
            if (provider.Connections.TryGetValue(connectionName, out var connection))
            {
#pragma warning disable CS0618 // Obsolete deployment name methods retained for backward compatibility
                deploymentName = connection.GetEmbeddingDeploymentOrDefaultName(false);
#pragma warning restore CS0618
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (string.IsNullOrEmpty(deploymentName))
        {
            _logger.LogInformation("No embedding deployment configured. Documents will be stored without embeddings for vector search.");
            return null;
        }

        var legacyGenerator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

        if (legacyGenerator == null)
        {
            _logger.LogWarning("Failed to create embedding generator for client {Client}, connection {Connection}, deployment {Deployment}. Documents will be stored without embeddings.",
                providerName, connectionName, deploymentName);
        }

        return legacyGenerator;
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
        using (var stream = file.OpenReadStream())
        {
            var mediaType = MediaTypeHelper.InferMediaType(extension, file.ContentType);
            var ingestionDoc = await reader.ReadAsync(stream, file.FileName, mediaType);

            text = string.Join('\n', ingestionDoc.EnumerateContent()
                .Select(e => e.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document processing: no text content extracted from file '{FileName}'.", file.FileName.SanitizeLogValue());
            }

            return DocumentProcessingResult.Failed("Could not extract text content from the document.");
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Document processing: extracted {TextLength} chars from '{FileName}' (extension: {Extension}).", text.Length, file.FileName.SanitizeLogValue(), extension);
        }

        // Normalize the extracted text to strip HTML, Markdown, and extraneous formatting
        // before chunking and embedding, producing cleaner vectors and reducing token usage.
        text = await RagTextNormalizer.NormalizeContentAsync(text);

        var now = _clock.UtcNow;

        var document = new AIDocument
        {
            ItemId = IdGenerator.GenerateId(),
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
            var textChunks = await RagTextNormalizer.NormalizeAndChunkAsync(text);
            textChunks = LimitChunksForEmbedding(textChunks);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document processing: generated {ChunkCount} chunk(s) for '{FileName}'.", textChunks.Count, file.FileName);
            }

            // Generate embeddings for all chunks in a single batch.
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
                    ItemId = IdGenerator.GenerateId(),
                    AIDocumentId = document.ItemId,
                    ReferenceId = referenceId,
                    ReferenceType = referenceType,
                    Content = textChunks[i],
                    Embedding = embeddings != null && i < embeddings.Count ? embeddings[i].Vector.ToArray() : null,
                    Index = i,
                });
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Document processing: created {ChunkCount} chunk record(s) for '{FileName}'.", chunks.Count, file.FileName);
            }
        }
        else if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Document processing: skipping embedding generation for '{FileName}' (extension={Extension}, textLength={TextLength}, hasGenerator={HasGenerator}).",
                file.FileName.SanitizeLogValue(), extension, text.Length, embeddingGenerator != null);
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
