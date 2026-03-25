using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Indexing;

namespace CrestApps.OrchardCore.AI.Documents.Endpoints;

internal static class RemoveDocumentEndpoint
{
    public static IEndpointRouteBuilder AddRemoveDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/chat-interactions/remove-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatInteractionRemoveDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] RemoveDocumentRequest request,
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] ICatalogManager<ChatInteraction> interactionManager,
        [FromServices] IAIDocumentStore documentStore,
        [FromServices] IAIDocumentChunkStore chunkStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions))
        {
            return TypedResults.Forbid();
        }

        if (request == null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrEmpty(request.ItemId) || string.IsNullOrEmpty(request.DocumentId))
        {
            return TypedResults.BadRequest("Item ID and Document ID are required.");
        }

        var interaction = await interactionManager.FindByIdAsync(request.ItemId);

        if (interaction == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions, interaction))
        {
            return TypedResults.Forbid();
        }

        // Find and remove the document info from the interaction
        var documentInfo = interaction.Documents?.FirstOrDefault(d => d.DocumentId == request.DocumentId);

        if (documentInfo == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        interaction.Documents.Remove(documentInfo);

        // Remove the document, its chunks, and vector index entries.
        var document = await documentStore.FindByIdAsync(request.DocumentId);
        if (document != null)
        {
            var chunks = await chunkStore.GetChunksByAIDocumentIdAsync(document.ItemId);
            var chunkIdsToRemove = chunks.Select(c => c.ItemId).ToList();

            if (chunkIdsToRemove.Count > 0)
            {
                ShellScope.AddDeferredTask(scope => RemoveDocumentChunksAsync(scope, chunkIdsToRemove));
            }

            await chunkStore.DeleteByDocumentIdAsync(document.ItemId);
            await documentStore.DeleteAsync(document);
        }

        // Save the interaction
        await interactionManager.UpdateAsync(interaction);

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
}
