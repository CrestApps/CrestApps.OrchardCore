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
/// <see cref="IInboundVoiceService"/> directly.
/// </summary>
[ApiController]
[Authorize]
[Route("api/contact-center/voice")]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceIngressController : ControllerBase
{
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceIngressController"/> class.
    /// </summary>
    /// <param name="inboundVoiceService">The inbound voice service that routes the call.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public VoiceIngressController(
        IInboundVoiceService inboundVoiceService,
        IAuthorizationService authorizationService)
    {
        _inboundVoiceService = inboundVoiceService;
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

        var result = await _inboundVoiceService.HandleInboundAsync(inboundEvent, HttpContext.RequestAborted);

        return Ok(result);
    }
}
