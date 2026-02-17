using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class RemoveProfileDocumentEndpoint
{
    public static IEndpointRouteBuilder AddRemoveProfileDocumentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("ai/profiles/remove-document", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.AIProfileRemoveDocument)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        RemoveProfileDocumentRequest request,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IAIProfileManager profileManager,
        IAIProfileDocumentStore profileDocumentStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        if (request == null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrEmpty(request.ProfileId) || string.IsNullOrEmpty(request.DocumentId))
        {
            return TypedResults.BadRequest("Profile ID and Document ID are required.");
        }

        var profile = await profileManager.FindByIdAsync(request.ProfileId);

        if (profile == null)
        {
            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles, profile))
        {
            return TypedResults.Forbid();
        }

        var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();

        var documentInfo = documentsMetadata.Documents?.FirstOrDefault(d => d.DocumentId == request.DocumentId);

        if (documentInfo == null)
        {
            return TypedResults.NotFound("Document not found.");
        }

        documentsMetadata.Documents.Remove(documentInfo);

        var document = await profileDocumentStore.FindByIdAsync(request.DocumentId);
        if (document != null)
        {
            await profileDocumentStore.DeleteAsync(document);
        }

        profile.Put(documentsMetadata);
        await profileManager.UpdateAsync(profile);

        return TypedResults.Ok();
    }
}

public sealed class RemoveProfileDocumentRequest
{
    public string ProfileId { get; set; }
    public string DocumentId { get; set; }
}
