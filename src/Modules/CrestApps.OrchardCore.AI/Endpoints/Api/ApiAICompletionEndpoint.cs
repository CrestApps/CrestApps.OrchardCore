using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAICompletionEndpoint
{
    public static IEndpointRouteBuilder AddApiAICompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("api/ai/completion/chat", AICompletionEndpoint.HandleAsync<T>)
            .DisableAntiforgery()
            .RequireCors(AIConstants.AiCompletionChatPolicyName)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }
}
