using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Liquid;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class AIChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddAIChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("AI/Chat/Session", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.AIChatSessionRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        IAuthorizationService authorizationService,
        IAIProfileManager chatProfileManager,
        IAIChatSessionManager sessionManager,
        ILiquidTemplateManager liquidTemplateManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IAIMarkdownService markdownService,
        string sessionId,
        bool includeHtmlResponse = true)
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

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IAIChatCompletionService>(profile.Source);

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
            Messages = chatSession.Prompts.Select(message => new AIChatResponseMessageDetailed
            {
                Id = message.Id,
                Role = message.Role.Value,
                IsGeneratedPrompt = message.IsGeneratedPrompt,
                Title = message.Title,
                Content = message.Content,
                HtmlContent = includeHtmlResponse && !string.IsNullOrEmpty(message.Content)
                ? markdownService.ToHtml(message.Content)
                : null,
            })
        });
    }
}
