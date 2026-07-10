using System.Security.Cryptography;
using System.Text.Json;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.DialPad.Controllers;

/// <summary>
/// Receives DialPad call-event webhooks and drives the Contact Center: it validates the DialPad
/// signature, then updates existing interactions or routes new inbound calls to an available agent.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/dialpad/webhook")]
[Feature(DialPadConstants.Feature.Dialer)]
public sealed class DialPadWebhookController : ControllerBase
{
    private readonly IDialPadWebhookService _webhookService;
    private readonly ISiteService _siteService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadWebhookController"/> class.
    /// </summary>
    /// <param name="webhookService">The DialPad webhook service.</param>
    /// <param name="siteService">The site service used to read DialPad settings.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the webhook secret.</param>
    /// <param name="logger">The logger instance.</param>
    public DialPadWebhookController(
        IDialPadWebhookService webhookService,
        ISiteService siteService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DialPadWebhookController> logger)
    {
        _webhookService = webhookService;
        _siteService = siteService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <summary>
    /// Handles a DialPad call-event webhook.
    /// </summary>
    /// <returns>An HTTP result describing whether the event was accepted.</returns>
    [HttpPost("call")]
    public async Task<IActionResult> Call()
    {
        var settings = await _siteService.GetSettingsAsync<DialPadSettings>();

        if (!settings.IsEnabled)
        {
            return NotFound();
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);

        if (string.IsNullOrEmpty(settings.WebhookSigningSecret))
        {
            _logger.LogWarning("Rejected a DialPad webhook because no webhook signing secret is configured.");

            return Unauthorized();
        }

        if (!TryUnprotectSecret(settings.WebhookSigningSecret, out var secret))
        {
            _logger.LogError("Rejected a DialPad webhook because the configured signing secret could not be unprotected.");

            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!DialPadJwtValidator.TryValidateAndExtract(body, secret, out var payloadJson))
        {
            _logger.LogWarning("Rejected a DialPad webhook because the signature could not be validated.");

            return Unauthorized();
        }

        DialPadCallEvent callEvent;

        try
        {
            callEvent = JsonSerializer.Deserialize<DialPadCallEvent>(payloadJson, DialPadJsonSerializerOptions.Default);
        }
        catch (JsonException)
        {
            return BadRequest();
        }

        if (callEvent is null)
        {
            return BadRequest();
        }

        var result = await _webhookService.ProcessAsync(callEvent, HttpContext.RequestAborted);

        return Ok(new { result = result.ToString() });
    }

    private bool TryUnprotectSecret(string protectedSecret, out string secret)
    {
        secret = null;

        try
        {
            var protector = _dataProtectionProvider.CreateProtector(DialPadConstants.WebhookProtectorName);
            secret = protector.Unprotect(protectedSecret);

            return !string.IsNullOrEmpty(secret);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
