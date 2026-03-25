using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

internal static class RemoveSessionDocumentEndpoint
{
    public static IEndpointRouteBuilder AddRemoveSessionDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/chat-sessions/remove-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatSessionRemoveDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest httpRequest,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAIChatSessionManager sessionManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (!await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile))
        {
            return TypedResults.Forbid();
        }

        RemoveDocumentRequest request;

        try
        {
            request = await httpRequest.ReadFromJsonAsync<RemoveDocumentRequest>();
        }
        catch
        {
            return TypedResults.BadRequest("Invalid request body.");
        }

        if (request == null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrEmpty(request.ItemId) || string.IsNullOrEmpty(request.DocumentId))
        {
            return TypedResults.BadRequest("Item ID and Document ID are required.");
        }

        var session = await sessionManager.FindAsync(request.ItemId);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var profile = await GetProfileAsync(httpContext.RequestServices, session.ProfileId);

        if (profile is null ||
            !await authorizationService.AuthorizeAsync(httpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var documentInfo = session.Documents?.FirstOrDefault(d => d.DocumentId == request.DocumentId);

        if (documentInfo == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        session.Documents.Remove(documentInfo);

        var document = await documentStore.FindByIdAsync(request.DocumentId);
        if (document != null)
        {
            // Get chunk IDs from the chunk store and schedule removal from vector index.
            var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);
            var chunkIdsToRemove = chunks.Select(c => c.ItemId).ToList();

            if (chunkIdsToRemove.Count > 0)
            {
                ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIdsToRemove));
            }

            // Delete chunks and document from store.
            await chunkStore.DeleteByDocumentIdAsync(document.ItemId);
            await documentStore.DeleteAsync(document);
        }

        await sessionManager.SaveAsync(session);

        return TypedResults.Ok();
    }

    private static async Task RemoveDocumentChunksAsync(ShellScope scope, List<string> chunkIds)
    {
        var services = scope.ServiceProvider;
        var indexStore = services.GetRequiredService<IIndexProfileStore>();
        var indexProfiles = await indexStore.GetByTypeAsync(AIConstants.AIDocumentsIndexingTaskType);

        if (!indexProfiles.Any())
        {
            return;
        }

        foreach (var indexProfile in indexProfiles)
        {
            var documentIndexManager = services.GetKeyedService<IDocumentIndexManager>(indexProfile.ProviderName);

            if (documentIndexManager == null)
            {
                continue;
            }

            await documentIndexManager.DeleteDocumentsAsync(indexProfile, chunkIds);
        }
    }

    private static async Task<AIProfile> GetProfileAsync(IServiceProvider serviceProvider, string profileId)
    {
        var profileManager = serviceProvider.GetService(typeof(IAIProfileManager)) as IAIProfileManager;
        return profileManager != null ? await profileManager.FindByIdAsync(profileId) : null;
    }
}
