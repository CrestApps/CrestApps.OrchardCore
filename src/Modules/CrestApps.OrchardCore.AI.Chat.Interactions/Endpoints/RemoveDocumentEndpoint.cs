using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Endpoints;

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
        IDocumentEmbeddingService embeddingService)
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

        // Find and remove the document
        var document = interaction.Documents?.FirstOrDefault(d => d.DocumentId == request.DocumentId);
        if (document == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        interaction.Documents.Remove(document);

        // Save the interaction
        await interactionManager.UpdateAsync(interaction);

        // Remove document from the index
        try
        {
            await embeddingService.RemoveDocumentAsync(interaction.ItemId, request.DocumentId);
        }
        catch
        {
            // Index removal failure should not block document removal from interaction
        }

        return TypedResults.Ok();
    }
}
