using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Endpoints;

internal static class UploadDocumentEndpoint
{
    // Target chunk size in characters (approximately 500 tokens)
    private const int ChunkSize = 2000;
    private const int ChunkOverlap = 200;

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
        var itemId = form["itemId"].ToString();
        var file = form.Files.GetFile("file");

        if (string.IsNullOrEmpty(itemId))
        {
            return TypedResults.BadRequest("Item ID is required.");
        }

        if (file == null || file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded.");
        }

        var interaction = await interactionManager.FindByIdAsync(itemId);
        if (interaction == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            return TypedResults.Forbid();
        }

        var extension = Path.GetExtension(file.FileName);

        // Check if file type is supported
        if (!extractorOptions.Value.AllowedFileExtensions.Contains(extension))
        {
            return TypedResults.BadRequest(S["File type '{0}' is not supported. Please upload text-based files (TXT, CSV, MD, JSON, XML, HTML).", Path.GetExtension(file.FileName)].Value);
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

        if (string.IsNullOrEmpty(deploymentName))
        {
            logger.LogError("Unable to find Embedding Deployment Name for the");

            return TypedResults.InternalServerError();
        }

        var embeddingGenerator = await aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

        if (embeddingGenerator == null)
        {
            logger.LogError("Failed to create embedding generator for provider {Provider}, connection {Connection}, deployment {Deployment}",
                interaction.Source, connectionName, deploymentName);

            return TypedResults.InternalServerError();
        }

        // Extract text from document.
        var content = new StringBuilder();

        if (textExtractors.Any())
        {
            using var stream = file.OpenReadStream();

            foreach (var textExtractor in textExtractors)
            {
                stream.Seek(0, SeekOrigin.Begin);

                var extractedContent = await textExtractor.ExtractAsync(stream, file.FileName, file.ContentType, Path.GetExtension(file.FileName));

                if (string.IsNullOrWhiteSpace(extractedContent))
                {
                    continue;
                }

                content.AppendLine(extractedContent);
            }
        }

        if (content.Length == 0)
        {
            return TypedResults.BadRequest(S["Could not extract text content from the document."].Value);
        }

        var now = clock.UtcNow;

        var text = content.ToString();

        interaction.Documents ??= [];

        var document = new ChatInteractionDocument
        {
            DocumentId = $"{interaction.ItemId}_{interaction.DocumentIndex++}",
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Content = text,
            UploadedUtc = now,
            ContentChunks = [],
        };

        var textChunks = ChunkText(text);

        if (textChunks.Count > 0)
        {
            var embedding = await embeddingGenerator.GenerateAsync(textChunks);

            for (var i = 0; i < textChunks.Count; i++)
            {
                document.ContentChunks.Add(new DocumentChunk
                {
                    Content = textChunks[i],
                    Embedding = embedding[i].Vector.ToArray(),
                    Index = i,
                });
            }
        }

        interaction.Documents.Add(document);

        await interactionManager.UpdateAsync(interaction);

        return TypedResults.Ok(new
        {
            documentId = document.DocumentId,
            fileName = document.FileName,
            fileSize = document.FileSize,
            uploadedUtc = document.UploadedUtc,
        });
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
