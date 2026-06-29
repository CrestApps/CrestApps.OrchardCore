using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Handles the agent-facing offer lifecycle for inbound voice calls. The soft-phone incoming-call
/// modal posts to these actions when the agent answers or ignores a ringing inbound call.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Voice)]
public sealed class VoiceController : Controller
{
    private readonly IActivityReservationService _reservationService;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly IInteractionManager _interactionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceController"/> class.
    /// </summary>
    /// <param name="reservationService">The reservation service used to accept or reject the offer.</param>
    /// <param name="inboundVoiceService">The inbound voice service used to re-offer a declined call.</param>
    /// <param name="interactionManager">The interaction manager used to update the interaction state.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="clock">The clock used to stamp answer times.</param>
    public VoiceController(
        IActivityReservationService reservationService,
        IInboundVoiceService inboundVoiceService,
        IInteractionManager interactionManager,
        IAuthorizationService authorizationService,
        IClock clock)
    {
        _reservationService = reservationService;
        _inboundVoiceService = inboundVoiceService;
        _interactionManager = interactionManager;
        _authorizationService = authorizationService;
        _clock = clock;
    }

    /// <summary>
    /// Accepts the offered inbound call: converts the reservation into an assignment and marks the
    /// interaction connected.
    /// </summary>
    /// <param name="reservationId">The reservation identifier of the offered call.</param>
    /// <returns>An empty success result, or a problem result when the offer is no longer valid.</returns>
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

        var reservation = await _reservationService.AcceptAsync(reservationId);

        if (reservation is null)
        {
            return NotFound();
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId);

        if (interaction is not null)
        {
            interaction.Status = InteractionStatus.Connected;
            interaction.StartedUtc ??= _clock.UtcNow;
            interaction.AnsweredUtc = _clock.UtcNow;
            await _interactionManager.UpdateAsync(interaction);
        }

        return Ok();
    }

    /// <summary>
    /// Declines the offered inbound call: returns the call to its queue and re-offers it to the next
    /// available agent.
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

        var reservation = await _reservationService.RejectAsync(reservationId);

        if (reservation is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(reservation.QueueId))
        {
            await _inboundVoiceService.OfferNextAsync(reservation.QueueId);
        }

        return Ok();
    }
}
