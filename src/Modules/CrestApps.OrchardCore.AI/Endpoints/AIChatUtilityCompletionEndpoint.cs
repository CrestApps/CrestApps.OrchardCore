using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class AIChatUtilityCompletionEndpoint
{
    public static IEndpointRouteBuilder AddAIUtilityCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("AI/Chat/UtilityCompletion", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.ChatUtilityCompletionRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IAIProfileManager chatProfileManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IAIMarkdownService markdownService,
        ILogger<T> logger,
        OpenAIChatUtilityCompletionRequest requestData)
    {
        if (string.IsNullOrWhiteSpace(requestData.ProfileId))
        {
            return TypedResults.BadRequest("ProfileId is required.");
        }

        if (string.IsNullOrWhiteSpace(requestData.Prompt))
        {
            return TypedResults.BadRequest("Prompt is required.");
        }

        var profile = await chatProfileManager.FindByIdAsync(requestData.ProfileId);

        if (profile is null)
        {
            return TypedResults.NotFound();
        }

        if (profile.Type != AIProfileType.Utility)
        {
            logger.LogWarning("The requested profile '{ProfileId}' has a type of '{ProfileType}', but it must be of type 'Utility' to use the utility-completion endpoint.", profile.Id, profile.Type.ToString());

            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        var completion = await completionService.ChatAsync([new ChatMessage(ChatRole.User, requestData.Prompt.Trim())], new AIChatCompletionContext()
        {
            Profile = profile,
            UserMarkdownInResponse = requestData.IncludeHtmlResponse,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new AIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(AIProfileType.Utility),
            Message = new AIChatResponseMessageDetailed
            {
                Content = bestChoice?.Text ?? AIConstants.DefaultBlankMessage,
                HtmlContent = requestData.IncludeHtmlResponse && !string.IsNullOrEmpty(bestChoice?.Text)
                ? markdownService.ToHtml(bestChoice.Text)
                : null,
            },
        });
    }

    private sealed class OpenAIChatUtilityCompletionRequest
    {
        public string ProfileId { get; set; }

        public string Prompt { get; set; }

        public bool IncludeHtmlResponse { get; set; }
    }
}
