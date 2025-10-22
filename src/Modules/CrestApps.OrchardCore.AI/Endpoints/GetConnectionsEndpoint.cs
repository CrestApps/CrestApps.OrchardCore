using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetConnectionsEndpoint
{
    public static IEndpointRouteBuilder AddGetConnectionsEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("ai/connections", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.GetConnectionsByProviderRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIProviderOptions> aiProviderOptions,
        string providerName)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return TypedResults.BadRequest("providerName is required.");
        }

        if (!aiProviderOptions.Value.Providers.TryGetValue(providerName, out var provider))
        {
            return TypedResults.BadRequest("invalid providerName.");
        }

        return TypedResults.Ok(provider.Connections.Select(x => new
        {
            Id = x.Key,
        }));
    }
}
