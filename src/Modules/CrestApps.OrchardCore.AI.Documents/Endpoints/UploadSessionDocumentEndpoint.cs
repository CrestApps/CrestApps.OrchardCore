using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Documents.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

internal static class UploadSessionDocumentEndpoint
{
    public static IEndpointRouteBuilder AddUploadSessionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/chat-sessions/upload-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatSessionUploadDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IAIChatSessionManager sessionManager,
        IAIDocumentStore documentStore,
        IAIDocumentProcessingService documentProcessingService,
        IOptions<ChatDocumentsOptions> extractorOptions,
        ILogger<Startup> logger,
        IStringLocalizer<Startup> S)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile))
        {
            return TypedResults.Forbid();
        }

        var form = await request.ReadFormAsync();
        var sessionId = form["sessionId"].ToString();
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

        if (string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(profileId))
        {
            return TypedResults.BadRequest("Session ID or Profile ID is required.");
        }

        if (files.Count == 0)
        {
            return TypedResults.BadRequest("No files uploaded.");
        }

        AIChatSession session;
        AIProfile profile;
        var isNewSession = false;

        if (!string.IsNullOrEmpty(sessionId))
        {
            session = await sessionManager.FindAsync(sessionId);
            if (session == null)
            {
                return TypedResults.NotFound();
            }

            profile = await GetProfileAsync(httpContext.RequestServices, session.ProfileId);
        }
        else
        {
            // No session yet — create one from the profile.
            profile = await GetProfileAsync(httpContext.RequestServices, profileId);

            if (profile is null ||
                !await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
            {
                return TypedResults.Forbid();
            }

            session = await sessionManager.NewAsync(profile, new NewAIChatSessionContext());
            session.Title = AIConstants.DefaultBlankSessionTitle;

            await sessionManager.SaveAsync(session);
            isNewSession = true;
        }

        if (profile is null ||
            !await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var embeddingGenerator = await documentProcessingService.CreateEmbeddingGeneratorAsync(profile.Source, profile.ConnectionName);

        session.Documents ??= [];

        var uploadedDocuments = new List<object>();
        var failedFiles = new List<object>();
        var processedDocuments = new List<AIDocument>();

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
                    session.SessionId,
                    AIConstants.DocumentReferenceTypes.ChatSession,
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
                session.Documents.Add(result.DocumentInfo);

                processedDocuments.Add(result.Document);
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

        await sessionManager.SaveAsync(session);

        // Schedule vector indexing of processed document chunks as a deferred task,
        // following the same pattern used by ChatInteractionHandler.
        if (processedDocuments.Count > 0)
        {
            var docs = processedDocuments.ToList();
            ShellScope.AddDeferredTask(scope => IndexDocumentChunksAsync(scope, docs));
        }

        return TypedResults.Ok(new
        {
            sessionId = session.SessionId,
            isNewSession,
            uploaded = uploadedDocuments,
            failed = failedFiles,
        });
    }

    private static async Task<AIProfile> GetProfileAsync(IServiceProvider serviceProvider, string profileId)
    {
        var profileManager = serviceProvider.GetService(typeof(IAIProfileManager)) as IAIProfileManager;
        return profileManager != null ? await profileManager.FindByIdAsync(profileId) : null;
    }

    private static async Task IndexDocumentChunksAsync(ShellScope scope, List<AIDocument> documents)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        var documentIndexHandlers = services.GetRequiredService<IEnumerable<IDocumentIndexHandler>>();
        var logger = services.GetRequiredService<ILogger<Startup>>();

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            var chunkDocuments = new List<DocumentIndex>();

            foreach (var aiDocument in documents)
            {
                if (aiDocument.Chunks == null || aiDocument.Chunks.Count == 0)
                {
                    continue;
                }

                foreach (var chunk in aiDocument.Chunks)
                {
                    var chunkId = $"{aiDocument.ItemId}_{chunk.Index}";
                    var documentIndex = new DocumentIndex(chunkId);

                    var aiDocumentChunk = new AIDocumentChunk
                    {
                        ChunkId = chunkId,
                        DocumentId = aiDocument.ItemId,
                        Content = chunk.Text,
                        FileName = aiDocument.FileName,
                        ReferenceId = aiDocument.ReferenceId,
                        ReferenceType = aiDocument.ReferenceType,
                        ChunkIndex = chunk.Index,
                        Embedding = chunk.Embedding,
                    };

                    var buildContext = new BuildDocumentIndexContext(documentIndex, aiDocumentChunk, [chunkId], documentIndexManager.GetContentIndexSettings())
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { nameof(IndexProfile), indexProfile },
                        }
                    };

                    await documentIndexHandlers.InvokeAsync((handler, ctx) => handler.BuildIndexAsync(ctx), buildContext, logger);

                    chunkDocuments.Add(documentIndex);
                }
            }

            if (chunkDocuments.Count > 0)
            {
                await documentIndexManager.AddOrUpdateDocumentsAsync(indexProfile, chunkDocuments);
            }
        }
    }
}
