using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Shipping.Endpoints;

/// <summary>
/// Hears EasyPost's tracking updates at <see cref="ShippingConstants.EasyPostWebhookPath"/>, the address the EasyPost
/// settings page shows. Only updates signed with the webhook secret are applied.
/// </summary>
internal static class EasyPostWebhookEndpoint
{
    public const string RouteName = "EasyPostWebhook";

    private const long MaxBodyBytes = 512 * 1024;

    public static IEndpointRouteBuilder AddEasyPostWebhookEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("/" + ShippingConstants.EasyPostWebhookPath, HandleAsync)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        ISiteService siteService,
        EasyPostCarrierProvider carrier,
        EasyPostTrackingUpdater updater,
        ISession session)
    {
        using var reader = new StreamReader(httpContext.Request.Body);
        var body = await reader.ReadToEndAsync(httpContext.RequestAborted);
        var secret = carrier.Unprotect((await siteService.GetSettingsAsync<EasyPostSettings>()).WebhookSecret);

        if (!EasyPostTrackingUpdater.IsSigned(body, httpContext.Request.Headers["X-Hmac-Signature"], secret))
        {
            return Results.Unauthorized();
        }

        JsonObject update;

        try
        {
            update = JsonNode.Parse(body) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.BadRequest();
        }

        // Only parcels' tracking matters here; EasyPost sends other events to the same address.
        if (update?["description"]?.GetValue<string>() is not ("tracker.updated" or "tracker.created"))
        {
            return Results.Ok();
        }

        try
        {
            if (await updater.ApplyAsync(update, httpContext.RequestAborted))
            {
                await session.SaveChangesAsync(httpContext.RequestAborted);
            }
        }
        catch (ConcurrencyException)
        {
            // EasyPost retries what isn't acknowledged.
            return Results.StatusCode(StatusCodes.Status409Conflict);
        }

        return Results.Ok();
    }
}
