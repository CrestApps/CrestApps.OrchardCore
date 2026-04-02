using CrestApps.AI.Chat.Services;
using CrestApps.AI.Deployments;
using CrestApps.AI.Models;
using CrestApps.AI.Profiles;
using CrestApps.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Chat.Endpoints;

/// <summary>
/// Registers reusable minimal API endpoints for uploading and removing documents
/// from chat interactions and AI chat sessions.
/// </summary>
public static class AIChatDocumentEndpoints
{
    /// <summary>
    /// Adds the chat interaction document upload endpoint.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder AddUploadChatInteractionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("ai/chat-interactions/upload-document", UploadChatInteractionDocumentAsync)
            .DisableAntiforgery();

        return builder;
    }
    /// <summary>
    /// Adds the chat interaction document removal endpoint.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder AddRemoveChatInteractionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("ai/chat-interactions/remove-document", RemoveChatInteractionDocumentAsync)
            .DisableAntiforgery();

        return builder;
    }
    /// <summary>
    /// Adds the chat session document upload endpoint.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder AddUploadChatSessionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("ai/chat-sessions/upload-document", UploadChatSessionDocumentAsync)
            .DisableAntiforgery();

        return builder;
    }
    /// <summary>
    /// Adds the chat session document removal endpoint.
    /// </summary>
    /// <param name="builder">The route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder AddRemoveChatSessionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("ai/chat-sessions/remove-document", RemoveChatSessionDocumentAsync)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> UploadChatInteractionDocumentAsync(
        HttpRequest request,
        [FromServices] ICatalogManager<ChatInteraction> interactionManager,
        [FromServices] IAIDeploymentManager deploymentManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore,
        [FromServices] IAIDocumentProcessingService documentProcessingService,
        [FromServices] IAIChatDocumentAuthorizationService authorizationService,
        [FromServices] IEnumerable<IAIChatDocumentEventHandler> eventHandlers,
        [FromServices] IOptions<ChatDocumentsOptions> documentOptions,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IStringLocalizerFactory localizerFactory)
    {
        var form = await request.ReadFormAsync();
        var interactionId = form["chatInteractionId"].ToString();
        var files = GetFiles(form);

        if (string.IsNullOrWhiteSpace(interactionId))
        {
            return TypedResults.BadRequest("Chat interaction ID is required.");
        }

        if (files.Count == 0)
        {
            return TypedResults.BadRequest("No files uploaded.");
        }

        var interaction = await interactionManager.FindByIdAsync(interactionId);
        if (interaction == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.CanManageChatInteractionDocumentsAsync(request.HttpContext.User, interaction))
        {
            return TypedResults.Forbid();
        }

        var deployment = await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: interaction.ChatDeploymentName);
        var embeddingGenerator = await documentProcessingService.CreateEmbeddingGeneratorAsync(deployment?.ClientName, deployment?.ConnectionName ?? interaction.ConnectionName);
        var logger = loggerFactory.CreateLogger("AIChatDocumentEndpoints");
        var S = localizerFactory.Create(typeof(AIChatDocumentEndpoints));

        interaction.Documents ??= [];

        var uploadedDocuments = new List<AIChatUploadedDocument>();
        var failedFiles = new List<object>();

        foreach (var file in files)
        {
            var result = await ProcessFileAsync(
                file,
                interaction.ItemId,
                AIReferenceTypes.Document.ChatInteraction,
                documentOptions.Value,
                documentProcessingService,
                embeddingGenerator,
                documentStore,
                chunkStore,
                logger,
                S);

            if (!result.Success)
            {
                failedFiles.Add(new { fileName = file.FileName, error = result.Error });
                continue;
            }

            interaction.Documents.Add(result.UploadedDocument.DocumentInfo);
            uploadedDocuments.Add(result.UploadedDocument);
        }

        await interactionManager.UpdateAsync(interaction);

        if (uploadedDocuments.Count > 0)
        {
            var context = new AIChatDocumentUploadContext
            {
                HttpContext = request.HttpContext,
                Interaction = interaction,
                ReferenceId = interaction.ItemId,
                ReferenceType = AIReferenceTypes.Document.ChatInteraction,
                UploadedDocuments = uploadedDocuments,
            };

            await InvokeUploadedHandlersAsync(eventHandlers, context, request.HttpContext.RequestAborted);
        }

        return TypedResults.Ok(new
        {
            uploaded = uploadedDocuments.Select(document => document.DocumentInfo),
            failed = failedFiles,
        });
    }

    private static async Task<IResult> RemoveChatInteractionDocumentAsync(
        [FromBody] RemoveDocumentRequest requestModel,
        HttpContext httpContext,
        [FromServices] ICatalogManager<ChatInteraction> interactionManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore,
        [FromServices] IAIChatDocumentAuthorizationService authorizationService,
        [FromServices] IEnumerable<IAIChatDocumentEventHandler> eventHandlers)
    {
        if (requestModel == null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(requestModel.ItemId) || string.IsNullOrWhiteSpace(requestModel.DocumentId))
        {
            return TypedResults.BadRequest("Item ID and document ID are required.");
        }

        var interaction = await interactionManager.FindByIdAsync(requestModel.ItemId);
        if (interaction == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.CanManageChatInteractionDocumentsAsync(httpContext.User, interaction))
        {
            return TypedResults.Forbid();
        }

        var documentInfo = interaction.Documents?.FirstOrDefault(document => document.DocumentId == requestModel.DocumentId);
        if (documentInfo == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        interaction.Documents.Remove(documentInfo);

        var document = await documentStore.FindByIdAsync(requestModel.DocumentId);
        var chunkIds = new List<string>();

        if (document != null)
        {
            var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);
            chunkIds = chunks.Select(chunk => chunk.ItemId).ToList();
            await chunkStore.DeleteByDocumentIdAsync(document.ItemId);
            await documentStore.DeleteAsync(document);
        }

        await interactionManager.UpdateAsync(interaction);

        var context = new AIChatDocumentRemoveContext
        {
            HttpContext = httpContext,
            Interaction = interaction,
            DocumentInfo = documentInfo,
            Document = document,
            ChunkIds = chunkIds,
            ReferenceId = interaction.ItemId,
            ReferenceType = AIReferenceTypes.Document.ChatInteraction,
        };

        await InvokeRemovedHandlersAsync(eventHandlers, context, httpContext.RequestAborted);

        return TypedResults.Ok();
    }

    private static async Task<IResult> UploadChatSessionDocumentAsync(
        HttpRequest request,
        [FromServices] IAIChatSessionManager sessionManager,
        [FromServices] IAIProfileManager profileManager,
        [FromServices] IAIDeploymentManager deploymentManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore,
        [FromServices] IAIDocumentProcessingService documentProcessingService,
        [FromServices] IAIChatDocumentAuthorizationService authorizationService,
        [FromServices] IEnumerable<IAIChatDocumentEventHandler> eventHandlers,
        [FromServices] IOptions<ChatDocumentsOptions> documentOptions,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IStringLocalizerFactory localizerFactory)
    {
        var form = await request.ReadFormAsync();
        var sessionId = form["sessionId"].ToString();
        var profileId = form["profileId"].ToString();
        var files = GetFiles(form);

        if (string.IsNullOrWhiteSpace(sessionId) && string.IsNullOrWhiteSpace(profileId))
        {
            return TypedResults.BadRequest("Session ID or profile ID is required.");
        }

        if (files.Count == 0)
        {
            return TypedResults.BadRequest("No files uploaded.");
        }

        AIChatSession session = null;
        AIProfile profile = null;
        var isNewSession = false;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = await sessionManager.FindAsync(sessionId);
            if (session == null)
            {
                return TypedResults.NotFound();
            }

            profile = await profileManager.FindByIdAsync(session.ProfileId);
        }
        else
        {
            profile = await profileManager.FindByIdAsync(profileId);
            if (profile == null)
            {
                return TypedResults.NotFound();
            }

            if (!IsSessionDocumentUploadEnabled(profile))
            {
                return TypedResults.BadRequest("Session document uploads are not enabled for this AI profile.");
            }

            session = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
            session.Title = "Untitled";
            session.UserId = request.HttpContext.User.Identity?.Name;

            await sessionManager.SaveAsync(session);
            isNewSession = true;
        }

        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        if (!IsSessionDocumentUploadEnabled(profile))
        {
            return TypedResults.BadRequest("Session document uploads are not enabled for this AI profile.");
        }

        if (!await authorizationService.CanManageChatSessionDocumentsAsync(request.HttpContext.User, profile, session))
        {
            return TypedResults.Forbid();
        }

        var deployment = await ResolveSessionDeploymentAsync(profile, deploymentManager);
        var embeddingGenerator = await documentProcessingService.CreateEmbeddingGeneratorAsync(deployment?.ClientName, deployment?.ConnectionName);
        var logger = loggerFactory.CreateLogger("AIChatDocumentEndpoints");
        var S = localizerFactory.Create(typeof(AIChatDocumentEndpoints));

        session.Documents ??= [];

        var uploadedDocuments = new List<AIChatUploadedDocument>();
        var failedFiles = new List<object>();

        foreach (var file in files)
        {
            var result = await ProcessFileAsync(
                file,
                session.SessionId,
                AIReferenceTypes.Document.ChatSession,
                documentOptions.Value,
                documentProcessingService,
                embeddingGenerator,
                documentStore,
                chunkStore,
                logger,
                S);

            if (!result.Success)
            {
                failedFiles.Add(new { fileName = file.FileName, error = result.Error });
                continue;
            }

            session.Documents.Add(result.UploadedDocument.DocumentInfo);
            uploadedDocuments.Add(result.UploadedDocument);
        }

        await sessionManager.SaveAsync(session);

        if (uploadedDocuments.Count > 0)
        {
            var context = new AIChatDocumentUploadContext
            {
                HttpContext = request.HttpContext,
                Session = session,
                Profile = profile,
                ReferenceId = session.SessionId,
                ReferenceType = AIReferenceTypes.Document.ChatSession,
                UploadedDocuments = uploadedDocuments,
                IsNewSession = isNewSession,
            };

            await InvokeUploadedHandlersAsync(eventHandlers, context, request.HttpContext.RequestAborted);
        }

        return TypedResults.Ok(new
        {
            sessionId = session.SessionId,
            isNewSession,
            uploaded = uploadedDocuments.Select(document => document.DocumentInfo),
            failed = failedFiles,
        });
    }

    private static async Task<IResult> RemoveChatSessionDocumentAsync(
        [FromBody] RemoveDocumentRequest requestModel,
        HttpContext httpContext,
        [FromServices] IAIChatSessionManager sessionManager,
        [FromServices] IAIProfileManager profileManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore,
        [FromServices] IAIChatDocumentAuthorizationService authorizationService,
        [FromServices] IEnumerable<IAIChatDocumentEventHandler> eventHandlers)
    {
        if (requestModel == null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(requestModel.ItemId) || string.IsNullOrWhiteSpace(requestModel.DocumentId))
        {
            return TypedResults.BadRequest("Item ID and document ID are required.");
        }

        var session = await sessionManager.FindAsync(requestModel.ItemId);
        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var profile = await profileManager.FindByIdAsync(session.ProfileId);
        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.CanManageChatSessionDocumentsAsync(httpContext.User, profile, session))
        {
            return TypedResults.Forbid();
        }

        var documentInfo = session.Documents?.FirstOrDefault(document => document.DocumentId == requestModel.DocumentId);
        if (documentInfo == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        session.Documents.Remove(documentInfo);

        var document = await documentStore.FindByIdAsync(requestModel.DocumentId);
        var chunkIds = new List<string>();

        if (document != null)
        {
            var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);
            chunkIds = chunks.Select(chunk => chunk.ItemId).ToList();
            await chunkStore.DeleteByDocumentIdAsync(document.ItemId);
            await documentStore.DeleteAsync(document);
        }

        await sessionManager.SaveAsync(session);

        var context = new AIChatDocumentRemoveContext
        {
            HttpContext = httpContext,
            Session = session,
            Profile = profile,
            DocumentInfo = documentInfo,
            Document = document,
            ChunkIds = chunkIds,
            ReferenceId = session.SessionId,
            ReferenceType = AIReferenceTypes.Document.ChatSession,
        };

        await InvokeRemovedHandlersAsync(eventHandlers, context, httpContext.RequestAborted);

        return TypedResults.Ok();
    }

    private static async Task<(bool Success, string Error, AIChatUploadedDocument UploadedDocument)> ProcessFileAsync(
        IFormFile file,
        string referenceId,
        string referenceType,
        ChatDocumentsOptions documentOptions,
        IAIDocumentProcessingService documentProcessingService,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IAIDocumentStore documentStore,
        IAIDocumentChunkStore chunkStore,
        ILogger logger,
        IStringLocalizer S)
    {
        if (file == null || file.Length == 0)
        {
            return (false, S["No file was uploaded."].Value, null);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!documentOptions.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (false, S["File type '{0}' is not supported.", extension].Value, null);
        }

        try
        {
            var result = await documentProcessingService.ProcessFileAsync(file, referenceId, referenceType, embeddingGenerator);
            if (!result.Success)
            {
                return (false, result.Error, null);
            }

            await documentStore.CreateAsync(result.Document);

            foreach (var chunk in result.Chunks)
            {
                await chunkStore.CreateAsync(chunk);
            }

            return (true, null, new AIChatUploadedDocument
            {
                File = file,
                Document = result.Document,
                DocumentInfo = result.DocumentInfo,
                Chunks = result.Chunks,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process file {FileName}", file.FileName);
            return (false, S["Failed to process file."].Value, null);
        }
    }

    private static IReadOnlyList<IFormFile> GetFiles(IFormCollection form)
    {
        var files = form.Files.GetFiles("files");
        if (files.Count > 0)
        {
            return files;
        }

        var singleFile = form.Files.GetFile("file");
        return singleFile == null ? [] : [singleFile];
    }

    private static bool IsSessionDocumentUploadEnabled(AIProfile profile)
        => profile.As<AIProfileSessionDocumentsMetadata>()?.AllowSessionDocuments == true;

    private static async Task<AIDeployment> ResolveSessionDeploymentAsync(AIProfile profile, IAIDeploymentManager deploymentManager)
    {
        return await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Chat, deploymentName: profile.ChatDeploymentName)
        ?? await deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.Utility, deploymentName: profile.UtilityDeploymentName);
    }

    private static async Task InvokeUploadedHandlersAsync(IEnumerable<IAIChatDocumentEventHandler> eventHandlers, AIChatDocumentUploadContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in eventHandlers)
        {
            await handler.UploadedAsync(context, cancellationToken);
        }
    }

    private static async Task InvokeRemovedHandlersAsync(IEnumerable<IAIChatDocumentEventHandler> eventHandlers, AIChatDocumentRemoveContext context, CancellationToken cancellationToken)
    {
        foreach (var handler in eventHandlers)
        {
            await handler.RemovedAsync(context, cancellationToken);
        }
    }
}
