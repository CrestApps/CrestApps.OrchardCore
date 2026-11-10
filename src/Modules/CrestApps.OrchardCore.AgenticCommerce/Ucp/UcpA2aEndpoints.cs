using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Commerce.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// UCP's Agent2Agent (A2A) binding: the store's agent card at <c>ucp/a2a/.well-known/agent-card.json</c>, and the
/// A2A JSON-RPC endpoint at <c>ucp/a2a</c>.
/// </summary>
public static class UcpA2aEndpoints
{
    /// <summary>
    /// Maps the A2A binding.
    /// </summary>
    /// <param name="routes">The endpoint route builder.</param>
    public static IEndpointRouteBuilder MapUcpA2a(this IEndpointRouteBuilder routes)
    {
        routes.MapGet(UcpA2a.AgentCardRoute, AgentCardAsync)
            .AllowAnonymous()
            .WithName("UcpA2aAgentCard");

        routes.MapPost(UcpA2a.Route, (HttpContext httpContext, UcpA2aMessageHandler handler) => handler.HandleAsync(httpContext))
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(UcpJsonRpc.Errors)
            .AddEndpointFilter<UcpRequestFilter>()
            .WithName("UcpA2a");

        return routes;
    }

    // The card is public and cached, like the profile that points to it.
    private static async Task<IResult> AgentCardAsync(HttpContext httpContext, UcpProfileBuilder profiles, AgentSiteUrls urls, ISiteService siteService)
    {
        var offers = new List<UcpProfileContext>();

        foreach (var version in UcpConstants.Versions)
        {
            offers.Add(await profiles.GetOfferAsync(version));
        }

        var store = await siteService.GetSettingsAsync<StoreProfileSettings>();
        var storeName = string.IsNullOrWhiteSpace(store?.Name) ? (await siteService.GetSiteSettingsAsync()).SiteName : store.Name;
        var body = UcpA2aAgentCard.Build(await urls.GetBaseUrlAsync(), storeName, offers).ToJsonString(JsonSerializerOptions.Web);
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)).AsSpan(0, 16))}\"";

        httpContext.Response.Headers.CacheControl = "public, max-age=300";
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(body, "application/json", Encoding.UTF8);
    }
}
