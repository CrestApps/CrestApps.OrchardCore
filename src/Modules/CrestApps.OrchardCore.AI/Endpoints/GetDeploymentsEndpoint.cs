using CrestApps.OrchardCore.AI.Azure.Core;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Endpoints;

internal static class GetDeploymentsEndpoint
{
    public static IEndpointRouteBuilder AddGetDeploymentsEndpoint(this IEndpointRouteBuilder builder)
    {
        _ = builder.MapGet("AI/Deployments/ByConnection", HandleAsync)
            .AllowAnonymous()
            .WithName(AIConstants.RouteNames.GetDeploymentsByConnectionRouteName)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IAIDeploymentManager deploymentManager,
        string source,
        string connection)
    {
        if (!await authorizationService.AuthorizeAsync(httpContextAccessor.HttpContext.User, AIChatPermissions.ManageAIChatProfiles))
        {
            return TypedResults.Forbid();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return TypedResults.BadRequest("Source is required.");
        }

        if (string.IsNullOrWhiteSpace(connection))
        {
            return TypedResults.BadRequest("Connection is required.");
        }

        var deployments = await deploymentManager.GetAsync(source, connection);

        return TypedResults.Ok(deployments.Select(x => new
        {
            x.Id,
            x.Name,
            x.CreatedUtc,
        }));
    }
}
