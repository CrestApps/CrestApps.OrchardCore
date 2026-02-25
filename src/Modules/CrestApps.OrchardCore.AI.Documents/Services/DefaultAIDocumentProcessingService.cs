using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
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
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultAIDocumentProcessingService(
        IServiceProvider serviceProvider,
        IOptions<ChatDocumentsOptions> extractorOptions,
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        ILogger<DefaultAIDocumentProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _extractorOptions = extractorOptions;
        _aiClientFactory = aiClientFactory;
        _providerOptions = providerOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IEmbeddingGenerator<string, Embedding<float>>> CreateEmbeddingGeneratorAsync(string providerName, string connectionName)
    {
        string deploymentName = null;

        if (_providerOptions.Value.Providers.TryGetValue(providerName, out var provider))
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connection))
            {
                deploymentName = connection.GetEmbeddingDeploymentOrDefaultName(false);
            }
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            _logger.LogInformation("No embedding deployment configured. Documents will be stored without embeddings for vector search.");
            return null;
        }

        var generator = await _aiClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

        if (generator == null)
        {
            _logger.LogWarning("Failed to create embedding generator for provider {Provider}, connection {Connection}, deployment {Deployment}. Documents will be stored without embeddings.",
                providerName, connectionName, deploymentName);
        }

        return generator;
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
            return DocumentProcessingResult.Failed("Could not extract text content from the document.");
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
            Text = text,
            UploadedUtc = now,
            Chunks = [],
        };

        if (ShouldGenerateEmbeddings(extension, text.Length, embeddingGenerator, options))
        {
            var textChunks = await RagTextNormalizer.NormalizeAndChunkAsync(text);
            textChunks = LimitChunksForEmbedding(textChunks);

            if (textChunks.Count > 0)
            {
                try
                {
                    var embedding = await embeddingGenerator.GenerateAsync(textChunks);

                    for (var i = 0; i < textChunks.Count; i++)
                    {
                        document.Chunks.Add(new ChatInteractionDocumentChunk
                        {
                            Text = textChunks[i],
                            Embedding = embedding[i].Vector.ToArray(),
                            Index = i,
                        });
                    }
                }
                catch (Exception embeddingEx)
                {
                    _logger.LogWarning(embeddingEx, "Failed to generate embeddings for file {FileName}. File will be stored without vector search support.", file.FileName);
                }
            }
        }

        var documentInfo = new ChatInteractionDocumentInfo
        {
            DocumentId = document.ItemId,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
        };

        return DocumentProcessingResult.Succeeded(document, documentInfo);
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
