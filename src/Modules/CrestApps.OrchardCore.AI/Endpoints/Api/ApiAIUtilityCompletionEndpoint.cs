using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Endpoints.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAIUtilityCompletionEndpoint
{
    public static IEndpointRouteBuilder AddApiAIUtilityCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("api/ai/completion/utility", HandleAsync<T>)
            .DisableAntiforgery()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }

    private static async Task<IResult> HandleAsync<T>(
       IAuthorizationService authorizationService,
       INamedCatalogManager<AIProfile> chatProfileManager,
       IHttpContextAccessor httpContextAccessor,
       IAICompletionService completionService,
       ILogger<T> logger,
       AIUtilityCompletionRequest requestData)
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

        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.QueryAnyAIProfile, profile))
        {
            return TypedResults.Forbid();
        }

        if (profile.Type != AIProfileType.Utility)
        {
            logger.LogWarning("The requested profile '{ProfileId}' has a type of '{ProfileType}', but it must be of type 'Utility' to use the utility-completion endpoint.", profile.Id, profile.Type.ToString());

            return TypedResults.NotFound();
        }

        var completion = await completionService.CompleteAsync(profile.Source, [new ChatMessage(ChatRole.User, requestData.Prompt.Trim())], new AICompletionContext()
        {
            Profile = profile,
        });

        var result = new AIChatResponse
        {
            Success = completion.Messages.Count > 0,
            Type = nameof(AIProfileType.Utility),
            Message = new AIChatResponseMessageDetailed(),
        };

        if (completion.AdditionalProperties is not null)
        {
            if (completion.AdditionalProperties.TryGetValue<Dictionary<string, AICompletionReference>>("References", out var referenceItems))
            {
                result.Message.References = referenceItems;
            }
        }

        result.Message.Content = completion.Messages.FirstOrDefault()?.Text ?? AIConstants.DefaultBlankMessage;

        return TypedResults.Ok(result);
    }
}
