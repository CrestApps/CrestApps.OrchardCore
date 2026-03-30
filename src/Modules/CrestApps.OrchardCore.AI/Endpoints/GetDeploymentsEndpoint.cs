using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetDeploymentsEndpoint
{
    public static IEndpointRouteBuilder AddGetDeploymentsEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("ai/deployments", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.GetDeploymentsByConnectionRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromServices] IAuthorizationService authorizationService,
        [FromServices] IHttpContextAccessor httpContextAccessor,
        [FromServices] IAIDeploymentManager deploymentManager,
        [FromQuery] string providerName,
        [FromQuery] string connection)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            return TypedResults.BadRequest("providerName is required.");
        }

        if (string.IsNullOrWhiteSpace(connection))
        {
            return TypedResults.BadRequest("Connection is required.");
        }

        var deployments = await deploymentManager.GetAllAsync(providerName, connection);

        return TypedResults.Ok(deployments.Select(x => new
        {
            Id = x.Name,
            x.ItemId,
            x.Name,
            x.ModelName,
            DisplayText = string.Equals(x.Name, x.ModelName, StringComparison.OrdinalIgnoreCase)
                ? x.Name
                : $"{x.Name} ({x.ModelName})",
            x.CreatedUtc,
        }));
    }
}
