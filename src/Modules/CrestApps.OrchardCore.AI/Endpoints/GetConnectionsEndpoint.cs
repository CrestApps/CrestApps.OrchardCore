using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetConnectionsEndpoint
{
    public static IEndpointRouteBuilder AddGetConnectionsEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("ai/connections", HandleAsync)
            .WithName(AIConstants.RouteNames.GetConnectionsByProviderRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] INamedSourceCatalog<AIProviderConnection> connectionsCatalog,
        [FromQuery] string providerName)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return TypedResults.BadRequest("providerName is required.");
        }

        var connections = await connectionsCatalog.GetAsync(providerName);

        return TypedResults.Ok(connections
            .OrderBy(connection => connection.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .Select(connection => new
        {
            Id = connection.ItemId,
            Name = connection.GetDisplayName(),
        }));
    }
}
