using System.Text;
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
using OrchardCore;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Endpoints;

internal static class UploadDocumentEndpoint
{
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
        IDocumentEmbeddingService embeddingService,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> providerOptions,
        ILogger<Startup> logger,
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

        // Extract text from document.
        var content = new StringBuilder();

        if (textExtractors.Any())
        {
            using var stream = file.OpenReadStream();

            foreach (var textExtractor in textExtractors)
            {
                var text = await textExtractor.ExtractAsync(stream, file.FileName, file.ContentType);

                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                content.AppendLine(text);
            }
        }

        if (content.Length == 0)
        {
            return TypedResults.BadRequest(S["Could not extract text content from the document."].Value);
        }

        var textContent = content.ToString();

        // Create document entry
        var document = new ChatInteractionDocument
        {
            DocumentId = IdGenerator.GenerateId(),
            FileName = file.FileName,
            ContentType = file.ContentType,
            Content = textContent,
            FileSize = file.Length,
            UploadedUtc = DateTime.UtcNow
        };

        // Add to interaction
        interaction.Documents ??= [];
        interaction.Documents.Add(document);

        // Save the interaction
        await interactionManager.UpdateAsync(interaction);

        // Index the document for vector search if embedding service is available
        try
        {
            var options = aiOptions.Value;
            var providers = providerOptions.Value;

            if (options.ProfileSources.TryGetValue(interaction.Source, out var profileSource))
            {
                var providerName = profileSource.ProviderName;
                var connectionName = interaction.ConnectionName;

                // Fall back to default connection if none specified
                if (string.IsNullOrEmpty(connectionName) && providers.Providers.TryGetValue(providerName, out var provider))
                {
                    connectionName = provider.DefaultConnectionName;
                }

                // Use deployment or fall back to a default embedding model
                var deploymentName = interaction.DeploymentId ?? "text-embedding-ada-002";

                if (!string.IsNullOrEmpty(connectionName))
                {
                    await embeddingService.IndexDocumentAsync(
                        interaction.ItemId,
                        document.DocumentId,
                        document.FileName,
                        textContent,
                        providerName,
                        connectionName,
                        deploymentName);
                }
            }
        }
        catch (Exception ex)
        {
            // Embedding/indexing failure should not block document upload
            // The document is still saved in the interaction
            logger.LogError(ex, "Failed to index uploaded document '{FileName}' for interaction '{ItemId}'.", document.FileName, interaction.ItemId);
        }

        return TypedResults.Ok(new
        {
            documentId = document.DocumentId,
            fileName = document.FileName,
            fileSize = document.FileSize,
            uploadedUtc = document.UploadedUtc
        });
    }
}
