using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Core.Services;
using CrestApps.OrchardCore.OpenAI.Endpoints.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Endpoints;

internal static class OpenAIChatUtilityCompletionEndpoint
{
    public static IEndpointRouteBuilder AddOpenAIChatUtilityCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("OpenAI/ChatGPT/UtilityCompletion", HandleAsync<T>)
            .AllowAnonymous()
            .WithName(OpenAIConstants.RouteNames.ChatUtilityCompletionRouteName)
            .DisableAntiforgery()
            .RequireCors(OpenAIConstants.Security.ExternalChatCORSPolicyName);

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
        IAuthorizationService authorizationService,
        IOpenAIChatProfileManager chatProfileManager,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IOpenAIMarkdownService markdownService,
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

        if (profile.Type != OpenAIChatProfileType.Utility)
        {
            logger.LogWarning("The requested profile '{ProfileId}' has a type of '{ProfileType}', but it must be of type 'Utility' to use the utility-completion endpoint.", profile.Id, profile.Type.ToString());

            return TypedResults.NotFound();
        }

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, OpenAIChatPermissions.QueryAnyAIChatProfile, profile))
        {
            return TypedResults.Forbid();
        }

        var completionService = serviceProvider.GetKeyedService<IOpenAIChatCompletionService>(profile.Source);

        if (completionService is null)
        {
            return TypedResults.Problem($"Unable to find a chat completion service for the source: '{profile.Source}'.");
        }

        var completion = await completionService.ChatAsync([OpenAIChatCompletionMessage.CreateMessage(requestData.Prompt.Trim(), OpenAIConstants.Roles.User)], new OpenAIChatCompletionContext(profile)
        {
            SystemMessage = profile.SystemMessage,
            UserMarkdownInResponse = requestData.RespondWithHtml,
        });

        var bestChoice = completion.Choices.FirstOrDefault();

        return TypedResults.Ok(new OpenAIChatResponse
        {
            Success = completion.Choices.Any(),
            Type = nameof(OpenAIChatProfileType.Utility),
            Message = new OpenAIChatResponseMessage
            {
                Content = bestChoice?.Content ?? OpenAIConstants.DefaultBlankMessage,
                ContentHTML = requestData.RespondWithHtml && !string.IsNullOrEmpty(bestChoice?.Content)
                ? markdownService.ToHtml(bestChoice.Content)
                : OpenAIConstants.DefaultBlankMessage,
            },
        });
    }

    private sealed class OpenAIChatUtilityCompletionRequest
    {
        public string ProfileId { get; set; }

        public string Prompt { get; set; }

        public bool RespondWithHtml { get; set; }
    }
}
