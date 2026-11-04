using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Assets.Owners;

/// <summary>
/// The search behind the asset editor's owner pickers. It returns up to 50 <c>{ value, text }</c> items, the shape
/// the shared item selector reads.
/// </summary>
internal static class AssetOwnerEndpoints
{
    private const int MaxResults = 50;

    /// <summary>
    /// Maps the owner search endpoint.
    /// </summary>
    /// <param name="builder">The endpoint route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder AddAssetOwnerEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/api/crestapps/assets/owners/search", (Delegate)SearchAsync)
            .RequireAuthorization()
            .WithName("CrestApps.Assets.OwnerSearch");

        return builder;
    }

    internal static async Task<IResult> SearchAsync(
        AssetOwnerKind kind,
        string query,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        AssetOwnerDirectory owners)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, AssetsPermissions.ViewAssets))
        {
            return Results.Forbid();
        }

        if (owners.GetSource(kind) is not { } source)
        {
            return Results.Ok(Array.Empty<object>());
        }

        var found = await source.SearchAsync(query, MaxResults, httpContext.RequestAborted);

        return Results.Ok(found.Select(owner => new { value = owner.Id, text = owner.Name }));
    }
}
