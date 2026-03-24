using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Entities;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAIChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddApiAIChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("api/ai/chat/session", HandleAsync)
            .DisableAntiforgery()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }

    private static async Task<IResult> HandleAsync(
       [FromServices] IAuthorizationService authorizationService,
       [FromServices] INamedCatalogManager<AIProfile> profileManager,
       [FromServices] IAIChatSessionManager sessionManager,
       [FromServices] IAIChatSessionPromptStore promptStore,
       [FromServices] ILiquidTemplateManager liquidTemplateManager,
       [FromServices] IHttpContextAccessor httpContextAccessor,
       [FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return TypedResults.BadRequest();
        }

        var chatSession = await sessionManager.FindAsync(sessionId);

        if (chatSession is null)
        {
            return TypedResults.NotFound();
        }

        var profile = await profileManager.FindByIdAsync(chatSession.ProfileId);

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var prompts = await promptStore.GetPromptsAsync(chatSession.SessionId);

        return TypedResults.Ok(new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
            },
            Messages = prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.ItemId,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                References = message.References,
                Appearance = message.As<AssistantMessageAppearance>(),
            })
        });
    }
}
