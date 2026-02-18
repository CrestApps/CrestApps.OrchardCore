using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

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
        IAIDocumentStore documentStore,
        IAIDocumentProcessingService documentProcessingService,
        IOptions<ChatDocumentsOptions> extractorOptions,
        ILogger<Startup> logger,
        IStringLocalizer<Startup> S)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions))
        {
            return TypedResults.Forbid();
        }

        var form = await request.ReadFormAsync();
        var chatInteractionId = form["chatInteractionId"].ToString();
        var files = form.Files.GetFiles("files");

        // For backward compatibility, also support single file upload.
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

        var embeddingGenerator = await documentProcessingService.CreateEmbeddingGeneratorAsync(interaction.Source, interaction.ConnectionName);

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
                var result = await documentProcessingService.ProcessFileAsync(
                    file,
                    chatInteractionId,
                    AIConstants.DocumentReferenceTypes.ChatInteraction,
                    embeddingGenerator);

                if (!result.Success)
                {
                    failedFiles.Add(new
                    {
                        fileName = file.FileName,
                        error = result.Error
                    });
                    continue;
                }

                uploadedDocuments.Add(result.DocumentInfo);
                interaction.Documents.Add(result.DocumentInfo);

                await documentStore.CreateAsync(result.Document);
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
}
