using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Accepts signed provider voice webhooks. Each provider posts to its own route segment; the request is
/// authenticated by the provider adapter's signature check rather than by a user identity, so the
/// endpoint is anonymous but every delivery must carry a valid provider signature.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/contact-center/voice/webhook")]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class ProviderVoiceWebhookController : ControllerBase
{
    private readonly IProviderVoiceWebhookProcessor _processor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceWebhookController"/> class.
    /// </summary>
    /// <param name="processor">The provider voice webhook processor.</param>
    public ProviderVoiceWebhookController(IProviderVoiceWebhookProcessor processor)
    {
        _processor = processor;
    }

    /// <summary>
    /// Receives a signed webhook for the specified provider and ingests its normalized voice events.
    /// </summary>
    /// <param name="provider">The technical name of the provider that owns the webhook.</param>
    /// <returns>An HTTP result describing whether the webhook was accepted.</returns>
    [HttpPost("{provider}")]
    public async Task<IActionResult> Receive(string provider)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);

        var request = new ProviderVoiceWebhookRequest
        {
            Provider = provider,
            Body = body,
        };

        foreach (var header in Request.Headers)
        {
            request.Headers[header.Key] = header.Value.ToString();
        }

        foreach (var query in Request.Query)
        {
            request.Query[query.Key] = query.Value.ToString();
        }

        var outcome = await _processor.ProcessAsync(request, HttpContext.RequestAborted);

        return outcome.Status switch
        {
            ProviderVoiceWebhookStatus.Accepted => Ok(new { processed = outcome.ProcessedCount }),
            ProviderVoiceWebhookStatus.UnknownProvider => NotFound(),
            ProviderVoiceWebhookStatus.InvalidSignature => Unauthorized(),
            ProviderVoiceWebhookStatus.MissingIdempotencyKey => BadRequest(),
            _ => BadRequest(),
        };
    }
}
