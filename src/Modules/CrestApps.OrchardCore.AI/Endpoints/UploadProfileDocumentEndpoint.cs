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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class UploadProfileDocumentEndpoint
{
    private const int ChunkSize = 2000;
    private const int ChunkOverlap = 200;
    private const int MaxEmbeddingTotalChars = 25000;

    // Only text-based embeddable extensions for RAG.
    private static readonly HashSet<string> _tabularExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv", ".tsv", ".xls", ".xlsx",
    };

    public static IEndpointRouteBuilder AddUploadProfileDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/profiles/upload-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.AIProfileUploadDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IAIProfileManager profileManager,
        IAIProfileDocumentStore profileDocumentStore,
        IEnumerable<IDocumentTextExtractor> textExtractors,
        IOptions<ChatInteractionsOptions> extractorOptions,
        IAIClientFactory aIClientFactory,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<Startup> logger,
        IClock clock,
        IStringLocalizer<Startup> S)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        var form = await request.ReadFormAsync();
        var profileId = form["profileId"].ToString();
        var files = form.Files.GetFiles("files");

        if (files.Count == 0)
        {
            var singleFile = form.Files.GetFile("file");

            if (singleFile != null)
            {
                files = [singleFile];
            }
        }

        if (string.IsNullOrEmpty(profileId))
        {
            return TypedResults.BadRequest("Profile ID is required.");
        }

        if (files.Count == 0)
        {
            return TypedResults.BadRequest("No files uploaded.");
        }

        var profile = await profileManager.FindByIdAsync(profileId);
        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles, profile))
        {
            return TypedResults.Forbid();
        }

        var providerName = profile.Source;
        var connectionName = profile.ConnectionName;
        string deploymentName = null;

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

        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = null;
        if (!string.IsNullOrEmpty(deploymentName))
        {
            embeddingGenerator = await aIClientFactory.CreateEmbeddingGeneratorAsync(providerName, connectionName, deploymentName);

            if (embeddingGenerator == null)
            {
                logger.LogWarning("Failed to create embedding generator for provider {Provider}, connection {Connection}, deployment {Deployment}. Documents will be stored without embeddings.",
                    profile.Source, connectionName, deploymentName);
            }
        }
        else
        {
            logger.LogInformation("No embedding deployment configured. Documents will be stored without embeddings for vector search.");
        }

        var now = clock.UtcNow;
        var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();
        documentsMetadata.Documents ??= [];

        var uploadedDocuments = new List<object>();
        var failedFiles = new List<object>();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName);

            // Only allow text-based embeddable extensions for RAG (no tabular data).
            if (!extractorOptions.Value.AllowedFileExtensions.Contains(extension) ||
                _tabularExtensions.Contains(extension))
            {
                failedFiles.Add(new
                {
                    fileName = file.FileName,
                    error = S["File type '{0}' is not supported for AI Profile documents. Only text-based files are allowed.", extension].Value
                });
                continue;
            }

            try
            {
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

                var document = new AIProfileDocument
                {
                    ItemId = IdGenerator.GenerateId(),
                    ProfileId = profileId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    Text = text,
                    UploadedUtc = now,
                    Chunks = [],
                };

                var shouldEmbed = ShouldGenerateEmbeddings(extension, text.Length, embeddingGenerator, extractorOptions.Value);

                if (shouldEmbed)
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
                            logger.LogWarning(embeddingEx, "Failed to generate embeddings for file {FileName}. File will be stored without vector search support.", file.FileName);
                        }
                    }
                }

                var docInfo = new ChatInteractionDocumentInfo
                {
                    DocumentId = document.ItemId,
                    FileName = document.FileName,
                    FileSize = document.FileSize,
                    ContentType = document.ContentType,
                };

                uploadedDocuments.Add(docInfo);
                documentsMetadata.Documents.Add(docInfo);

                await profileDocumentStore.CreateAsync(document);
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

        profile.Put(documentsMetadata);
        await profileManager.UpdateAsync(profile);

        return TypedResults.Ok(new
        {
            uploaded = uploadedDocuments,
            failed = failedFiles,
        });
    }

    private static bool ShouldGenerateEmbeddings(
        string extension,
        int textLength,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ChatInteractionsOptions options)
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
