using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
        RemoveDocumentRequest request,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ISourceCatalogManager<ChatInteraction> interactionManager,
        IAIDocumentStore documentStore)
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

        // Remove the document from the document store
        var document = await documentStore.FindByIdAsync(request.DocumentId);
        if (document != null)
        {
            await documentStore.DeleteAsync(document);
        }

        // Save the interaction
        await interactionManager.UpdateAsync(interaction);

        return TypedResults.Ok();
    }
}
