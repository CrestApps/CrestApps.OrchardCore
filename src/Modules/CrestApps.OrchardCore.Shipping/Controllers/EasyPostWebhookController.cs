using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Shipping.Controllers;

/// <summary>
/// Hears EasyPost's tracking updates. Only updates signed with the webhook secret are applied.
/// </summary>
[Feature(ShippingConstants.Features.EasyPost)]
public sealed class EasyPostWebhookController : Controller
{
    private const long MaxBodyBytes = 512 * 1024;

    private readonly ISiteService _siteService;
    private readonly EasyPostCarrierProvider _carrier;
    private readonly EasyPostTrackingUpdater _updater;
    private readonly ISession _session;
    private readonly ILogger _logger;

    public EasyPostWebhookController(
        ISiteService siteService,
        EasyPostCarrierProvider carrier,
        EasyPostTrackingUpdater updater,
        ISession session,
        ILogger<EasyPostWebhookController> logger)
    {
        _siteService = siteService;
        _carrier = carrier;
        _updater = updater;
        _session = session;
        _logger = logger;
    }

    [HttpPost("shipping/easypost/webhook", Name = "EasyPostWebhook")]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Receive()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        var secret = _carrier.Unprotect((await _siteService.GetSettingsAsync<EasyPostSettings>()).WebhookSecret);

        if (!EasyPostTrackingUpdater.IsSigned(body, Request.Headers["X-Hmac-Signature"], secret))
        {
            return Unauthorized();
        }

        JsonObject update;

        try
        {
            update = JsonNode.Parse(body) as JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest();
        }

        // Only parcels' tracking matters here; EasyPost sends other events to the same address.
        if (update?["description"]?.GetValue<string>() is not ("tracker.updated" or "tracker.created"))
        {
            return Ok();
        }

        try
        {
            if (await _updater.ApplyAsync(update, HttpContext.RequestAborted))
            {
                await _session.SaveChangesAsync(HttpContext.RequestAborted);
            }
        }
        catch (ConcurrencyException)
        {
            // EasyPost retries what isn't acknowledged.
            return StatusCode(StatusCodes.Status409Conflict);
        }

        return Ok();
    }
}
