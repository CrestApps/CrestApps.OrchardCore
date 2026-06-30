using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Handles the agent-facing offer lifecycle for inbound voice calls. The soft-phone incoming-call
/// modal posts to these actions when the agent answers or ignores a ringing inbound call. Each action
/// delegates to <see cref="IContactCenterCallCommandService"/> so the reservation, media connection,
/// and interaction/call-session state are advanced together as one authoritative server-side command.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceController : Controller
{
    private readonly IContactCenterCallCommandService _callCommandService;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceController"/> class.
    /// </summary>
    /// <param name="callCommandService">The call command service that coordinates the offer lifecycle.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public VoiceController(
        IContactCenterCallCommandService callCommandService,
        IAuthorizationService authorizationService)
    {
        _callCommandService = callCommandService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Accepts the offered inbound call: accepts the reservation, connects the media to the agent, and
    /// marks the interaction connected as one server-side command.
    /// </summary>
    /// <param name="reservationId">The reservation identifier of the offered call.</param>
    /// <returns>The command result, or a problem result when the offer is no longer valid.</returns>
    [HttpPost]
    [Admin("contact-center/voice/offer/accept", "ContactCenterVoiceAcceptOffer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptOffer(string reservationId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(reservationId))
        {
            return BadRequest();
        }

        var result = await _callCommandService.AcceptInboundOfferAsync(reservationId);

        if (!result.Succeeded)
        {
            return NotFound();
        }

        return Ok(new
        {
            result.Succeeded,
            result.RequiresDeviceAnswer,
            result.InteractionId,
            result.CallSessionId,
        });
    }

    /// <summary>
    /// Declines the offered inbound call: rejects the reservation, returns the call to its queue, and
    /// re-offers it to the next available agent.
    /// </summary>
    /// <param name="reservationId">The reservation identifier of the offered call.</param>
    /// <returns>An empty success result, or a problem result when the offer is no longer valid.</returns>
    [HttpPost]
    [Admin("contact-center/voice/offer/decline", "ContactCenterVoiceDeclineOffer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineOffer(string reservationId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(reservationId))
        {
            return BadRequest();
        }

        var result = await _callCommandService.DeclineInboundOfferAsync(reservationId);

        if (!result.Succeeded)
        {
            return NotFound();
        }

        return Ok();
    }
}
