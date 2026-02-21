using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Services;

public sealed class DefaultAIDocumentProcessingService : IAIDocumentProcessingService
{
    private const int ChunkSize = 2000;
    private const int ChunkOverlap = 200;
    private const int MaxEmbeddingTotalChars = 25000;

    private readonly IEnumerable<IDocumentTextExtractor> _textExtractors;
    private readonly IOptions<ChatDocumentsOptions> _extractorOptions;
    private readonly IAIClientFactory _aiClientFactory;
    private readonly IOptions<AIProviderOptions> _providerOptions;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultAIDocumentProcessingService(
        IEnumerable<IDocumentTextExtractor> textExtractors,
        IOptions<ChatDocumentsOptions> extractorOptions,
        IAIClientFactory aiClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        IClock clock,
        ILogger<DefaultAIDocumentProcessingService> logger)
    {
        _textExtractors = textExtractors;
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

        var content = new StringBuilder();

        using var stream = file.OpenReadStream();

        foreach (var textExtractor in _textExtractors)
        {
            stream.Seek(0, SeekOrigin.Begin);

            var extractedContent = await textExtractor.ExtractAsync(stream, file.FileName, extension, file.ContentType);

            if (string.IsNullOrWhiteSpace(extractedContent))
            {
                continue;
            }

            content.AppendLine(extractedContent);
        }

        if (content.Length == 0)
        {
            return DocumentProcessingResult.Failed("Could not extract text content from the document.");
        }

        var text = content.ToString();
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
            var textChunks = ChunkText(text);
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

    private static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        var paragraphs = Regex.Split(text, @"\n\s*\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        var currentChunk = new StringBuilder();

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
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString());

                    var overlapText = GetOverlapText(currentChunk.ToString(), ChunkOverlap);
                    currentChunk.Clear();
                    if (!string.IsNullOrEmpty(overlapText))
                    {
                        currentChunk.Append(overlapText);
                        currentChunk.Append("\n\n");
                    }
                }

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

        var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var currentChunk = new StringBuilder();

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

                if (sentence.Length > ChunkSize)
                {
                    chunks.Add(sentence.Substring(0, Math.Min(sentence.Length, ChunkSize)));
                    var remaining = sentence.Substring(Math.Min(sentence.Length, ChunkSize));
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
