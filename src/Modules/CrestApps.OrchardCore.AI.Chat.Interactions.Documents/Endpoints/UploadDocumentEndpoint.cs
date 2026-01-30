using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Documents.Endpoints;

internal static class UploadDocumentEndpoint
{
    // Target chunk size in characters (approximately 500 tokens)
    private const int ChunkSize = 2000;
    private const int ChunkOverlap = 200;

    // Maximum total characters for embedding (to avoid token limit errors)
    // Most embedding models have 8192 token limit, ~4 chars per token = ~30000 chars max
    private const int MaxEmbeddingTotalChars = 25000;

    public static IEndpointRouteBuilder AddUploadDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/chat-interactions/upload-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatInteractionUploadDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IChatInteractionDocumentStore chatInteractionDocumentStore,
        IEnumerable<IDocumentTextExtractor> textExtractors,
        IOptions<ChatInteractionsOptions> extractorOptions,
        IAIClientFactory aIClientFactory,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<Startup> logger,
        IClock clock,
        IStringLocalizer<Startup> S)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions))
        {
            return TypedResults.Forbid();
        }

        var form = await request.ReadFormAsync();
        var chatInteractionId = form["chatInteractionId"].ToString();
        var files = form.Files.GetFiles("files");

        // For backward compatibility, also support single file upload
        if (files.Count == 0)
        {
            var singleFile = form.Files.GetFile("file");

            if (singleFile != null)
            {
                files = [singleFile];
            }
        }

        if (string.IsNullOrEmpty(chatInteractionId))
        {
            return TypedResults.BadRequest("Chat Interaction ID is required.");
        }

        if (files.Count == 0)
        {
            return TypedResults.BadRequest("No files uploaded.");
        }

        var interaction = await interactionManager.FindByIdAsync(chatInteractionId);
        if (interaction == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            return TypedResults.Forbid();
        }

        var providerName = interaction.Source;
        var connectionName = interaction.ConnectionName;
        string deploymentName = null;

        // Fall back to default connection if none specified
        if (providerOptions.Value.Providers.TryGetValue(providerName, out var provider))
        {
            if (string.IsNullOrEmpty(connectionName))
            {
                connectionName = provider.DefaultConnectionName;
            }

            if (!string.IsNullOrEmpty(connectionName) && provider.Connections.TryGetValue(connectionName, out var connection))
            {
                deploymentName = connection.GetDefaultEmbeddingDeploymentName(false);
            }
        }

        // Embedding generator is optional - only create if we have a deployment name
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = null;
        if (!string.IsNullOrEmpty(deploymentName))
        {
            embeddingGenerator = await aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

            if (embeddingGenerator == null)
            {
                logger.LogWarning("Failed to create embedding generator for provider {Provider}, connection {Connection}, deployment {Deployment}. Documents will be stored without embeddings.",
                    interaction.Source, connectionName, deploymentName);
            }
        }
        else
        {
            logger.LogInformation("No embedding deployment configured. Documents will be stored without embeddings for vector search.");
        }

        var now = clock.UtcNow;
        interaction.Documents ??= [];

        var uploadedDocuments = new List<object>();
        var failedFiles = new List<object>();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);

            // Check if file type is supported
            if (!extractorOptions.Value.AllowedFileExtensions.Contains(extension))
            {
                failedFiles.Add(new
                {
                    fileName = file.FileName,
                    error = S["File type '{0}' is not supported.", extension].Value
                });
                continue;
            }

            try
            {
                // Extract text from document.
                var content = new StringBuilder();

                if (textExtractors.Any())
                {
                    using var stream = file.OpenReadStream();

                    foreach (var textExtractor in textExtractors)
                    {
                        stream.Seek(0, SeekOrigin.Begin);

                        var extractedContent = await textExtractor.ExtractAsync(stream, file.FileName, extension, file.ContentType);

                        if (string.IsNullOrWhiteSpace(extractedContent))
                        {
                            continue;
                        }

                        content.AppendLine(extractedContent);
                    }
                }

                if (content.Length == 0)
                {
                    failedFiles.Add(new
                    {
                        fileName = file.FileName,
                        error = S["Could not extract text content from the document."].Value
                    });
                    continue;
                }

                var text = content.ToString();

                var document = new ChatInteractionDocument
                {
                    ItemId = IdGenerator.GenerateId(),
                    ChatInteractionId = chatInteractionId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Text = text,
                    UploadedUtc = now,
                    Chunks = [],
                };

                // Determine if we should generate embeddings for this file
                var shouldEmbed = ShouldGenerateEmbeddings(extension, text.Length, embeddingGenerator, extractorOptions.Value);

                if (shouldEmbed)
                {
                    var textChunks = ChunkText(text);

                    // Limit chunks to avoid exceeding token limits
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
                            // Log the error but continue without embeddings
                            logger.LogWarning(embeddingEx, "Failed to generate embeddings for file {FileName}. File will be stored without vector search support.", file.FileName);
                        }
                    }
                }
                else
                {
                    logger.LogDebug("Skipping embedding generation for file {FileName} (tabular data or too large)", file.FileName);
                }

                var docInfo = new ChatInteractionDocumentInfo
                {
                    DocumentId = document.ItemId,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    ContentType = document.ContentType,
                };

                uploadedDocuments.Add(docInfo);
                interaction.Documents.Add(docInfo);

                await chatInteractionDocumentStore.CreateAsync(document);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process file {FileName}", file.FileName);
                failedFiles.Add(new
                {
                    fileName = file.FileName,
                    error = S["Failed to process file."].Value
                });
            }
        }

        await interactionManager.UpdateAsync(interaction);

        return TypedResults.Ok(new
        {
            uploaded = uploadedDocuments,
            failed = failedFiles,
        });
    }

    /// <summary>
    /// Determines if embeddings should be generated for a file based on its type and size.
    /// </summary>
    private static bool ShouldGenerateEmbeddings(
        string extension,
        int textLength,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ChatInteractionsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // No embedding generator available
        if (embeddingGenerator == null)
        {
            return false;
        }

        // If extension isn't configured as embeddable, don't embed.
        // This allows admins/devs to explicitly set certain supported file types (e.g. CSV) as non-embeddable.
        if (!options.EmbeddableFileExtensions.Contains(extension))
        {
            return false;
        }

        // Skip very large files that would exceed token limits
        if (textLength > MaxEmbeddingTotalChars * 2)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Limits the chunks to stay within embedding token limits.
    /// </summary>
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

        // First, try to split by paragraphs (double newlines)
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

                // If a single sentence is too long, just add it as is
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
