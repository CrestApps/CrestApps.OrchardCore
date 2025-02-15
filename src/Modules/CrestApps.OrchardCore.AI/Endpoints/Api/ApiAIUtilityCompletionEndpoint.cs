using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints.Api;

internal static class ApiAIUtilityCompletionEndpoint
{
    public static IEndpointRouteBuilder AddApiAIUtilityCompletionEndpoint<T>(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapPost("api/ai/completion/utility", AIUtilityCompletionEndpoint.HandleAsync<T>)
            .DisableAntiforgery()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });

        return builder;
    }
}
