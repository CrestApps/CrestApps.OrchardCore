using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.OpenAI.Endpoints;

internal static class OpenAIChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddOpenAIChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("OpenAI/ChatGPT/Session", HandleAsync)
            .AllowAnonymous()
            .WithName(OpenAIConstants.RouteNames.ChatSessionRouteName)
            .DisableAntiforgery()
            .RequireCors(OpenAIConstants.Security.ExternalChatCORSPolicyName);

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        IAuthorizationService authorizationService,
        IOpenAIChatProfileManager chatProfileManager,
        IOpenAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IOpenAIMarkdownService markdownService,
        string sessionId)
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

        var profile = await chatProfileManager.FindByIdAsync(chatSession.ProfileId);

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OpenAIChatPermissions.QueryAnyAIChatProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IOpenAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        return TypedResults.Ok(new
        {
            chatSession.SessionId,
            Profile = new
            {
                Id = chatSession.ProfileId,
                Type = profile.Type.ToString()
            },
            Messages = chatSession.Prompts.Select(message => new
            {
                message.Id,
                message.Role,
                message.IsGeneratedPrompt,
                message.Title,
                message.Content,
                ContentHTML = !string.IsNullOrEmpty(message.Content)
                ? markdownService.ToHtml(message.Content)
                : null,
            })
        });
    }
}
