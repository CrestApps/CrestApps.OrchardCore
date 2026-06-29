using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provider-agnostic ingress for inbound voice events. A telephony provider or PBX integration posts
/// a normalized <see cref="InboundVoiceEvent"/> to this endpoint, which routes the call through the
/// Contact Center. Provider-specific webhooks that validate provider signatures may alternatively call
/// <see cref="IVoiceContactCenterCallRouter"/> directly.
/// </summary>
[ApiController]
[Authorize]
[Route("api/contact-center/voice")]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceIngressController : ControllerBase
{
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceIngressController"/> class.
    /// </summary>
    /// <param name="voiceCallRouter">The voice call router.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public VoiceIngressController(
        IVoiceContactCenterCallRouter voiceCallRouter,
        IAuthorizationService authorizationService)
    {
        _voiceCallRouter = voiceCallRouter;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Routes a normalized inbound voice event through the Contact Center.
    /// </summary>
    /// <param name="inboundEvent">The normalized inbound voice event.</param>
    /// <returns>The routing outcome describing the created records and the offered agent.</returns>
    [HttpPost("inbound")]
    public async Task<ActionResult<InboundVoiceRoutingResult>> Inbound([FromBody] InboundVoiceEvent inboundEvent)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageInteractions))
        {
            return Forbid();
        }

        if (inboundEvent is null)
        {
            return BadRequest();
        }

        var result = await _voiceCallRouter.RouteInboundAsync(inboundEvent, HttpContext.RequestAborted);

        return Ok(result);
    }
}
