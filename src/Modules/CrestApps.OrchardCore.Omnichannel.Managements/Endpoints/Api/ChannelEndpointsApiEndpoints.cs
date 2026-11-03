using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Endpoints.Api;

/// <summary>
/// Phone numbers and other channel addresses over the API: list, read, add, change and remove the addresses calls
/// and texts come from and arrive at. Buying a number stays with the carrier; this records it here.
/// </summary>
internal static class ChannelEndpointsApiEndpoints
{
    public static IEndpointRouteBuilder MapChannelEndpointsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("api/ai/v1/numbers")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = AIApiKeys.ApiSchemes })
            .DisableAntiforgery()
            .WithTags("Phone Numbers");

        group.MapGet(string.Empty, ListAsync)
            .WithName("ListChannelEndpoints")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the phone numbers and other channel addresses, optionally of one channel.");

        group.MapGet("{id}", GetAsync)
            .WithName("GetChannelEndpoint")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a phone number or other channel address.");

        group.MapPost(string.Empty, CreateAsync)
            .WithName("CreateChannelEndpoint")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Adds a phone number or other channel address: DisplayText, Channel, Value (the number in E.164), ProviderName and Capabilities.");

        group.MapPut("{id}", UpdateAsync)
            .WithName("UpdateChannelEndpoint")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Changes a phone number or other channel address, leaving what is not sent as it is.");

        group.MapDelete("{id}", DeleteAsync)
            .WithName("DeleteChannelEndpoint")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Removes a phone number or other channel address.");

        return routes;
    }

    private static async Task<Results<Ok<IReadOnlyList<OmnichannelChannelEndpoint>>, ProblemHttpResult>> ListAsync(
        string channel,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalogManager<OmnichannelChannelEndpoint> manager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbidden();
        }

        var endpoints = (await manager.GetAllAsync(httpContext.RequestAborted))
            .Where(endpoint => string.IsNullOrWhiteSpace(channel) || endpoint.HasCapability(channel) || string.Equals(endpoint.Channel, channel, StringComparison.OrdinalIgnoreCase))
            .OrderBy(endpoint => endpoint.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok<IReadOnlyList<OmnichannelChannelEndpoint>>(endpoints);
    }

    private static async Task<Results<Ok<OmnichannelChannelEndpoint>, ProblemHttpResult>> GetAsync(
        string id,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalogManager<OmnichannelChannelEndpoint> manager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbidden();
        }

        var endpoint = await manager.FindByIdAsync(id, httpContext.RequestAborted);

        return endpoint is null ? NotFound() : TypedResults.Ok(endpoint);
    }

    private static async Task<Results<Created<OmnichannelChannelEndpoint>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        JsonObject body,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalogManager<OmnichannelChannelEndpoint> manager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbidden();
        }

        if (body is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Send the address as JSON."] });
        }

        body.Remove(nameof(OmnichannelChannelEndpoint.ItemId));
        var endpoint = await manager.NewAsync(body, httpContext.RequestAborted);
        var validation = await manager.ValidateAsync(endpoint, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await manager.CreateAsync(endpoint, httpContext.RequestAborted);

        return TypedResults.Created($"/api/ai/v1/numbers/{endpoint.ItemId}", endpoint);
    }

    private static async Task<Results<Ok<OmnichannelChannelEndpoint>, ValidationProblem, ProblemHttpResult>> UpdateAsync(
        string id,
        JsonObject body,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalogManager<OmnichannelChannelEndpoint> manager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbidden();
        }

        var endpoint = await manager.FindByIdAsync(id, httpContext.RequestAborted);

        if (endpoint is null)
        {
            return NotFound();
        }

        body ??= [];
        body.Remove(nameof(OmnichannelChannelEndpoint.ItemId));

        // Validate a copy first: the manager saves as it updates.
        var current = JsonSerializer.SerializeToNode(endpoint) as JsonObject ?? [];
        var copy = await manager.NewAsync(AISourceCatalogApi.Merge(current, body), httpContext.RequestAborted);
        copy.ItemId = endpoint.ItemId;
        var validation = await manager.ValidateAsync(copy, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return AISourceCatalogApi.Invalid(validation);
        }

        await manager.UpdateAsync(endpoint, body, httpContext.RequestAborted);

        return TypedResults.Ok(endpoint);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string id,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalogManager<OmnichannelChannelEndpoint> manager)
    {
        if (!await AIApiKeys.AuthorizeAsync(authorizationService, httpContext, AIApiKeys.Scopes.Numbers, OmnichannelConstants.Permissions.ManageChannelEndpoints))
        {
            return Forbidden();
        }

        var endpoint = await manager.FindByIdAsync(id, httpContext.RequestAborted);

        if (endpoint is null)
        {
            return NotFound();
        }

        await manager.DeleteAsync(endpoint, httpContext.RequestAborted);

        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Forbidden()
        => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    private static ProblemHttpResult NotFound()
        => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found");
}
