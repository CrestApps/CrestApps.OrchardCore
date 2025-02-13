using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAIChatSessionEndpoint
{
    public static IEndpointRouteBuilder AddApiAIChatSessionEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("api/ai/chat/session", AIChatSessionEndpoint.HandleAsync)
            .DisableAntiforgery()
            .RequireCors(AIConstants.AiChatSessionPolicyName)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }
}
