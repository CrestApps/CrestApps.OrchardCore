using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Pushes "this offer was answered" to every connection the agent has, on both hubs, from inside the accept.
/// </summary>
/// <remarks>
/// A page listening on the Contact Center hub hears it as the same <c>OfferRevoked</c> (accepted) the outbox sends
/// a second later; a soft phone listening only on the Telephony hub -- another origin, the browser extension --
/// hears it as <c>IncomingCallAnswered</c>. Both are idempotent on the client, so the later durable copy is
/// harmless.
/// </remarks>
public sealed class ContactCenterOfferAnsweredNotifier : IContactCenterOfferAnsweredNotifier
{
    private readonly IContactCenterRealTimeNotifier _realTimeNotifier;
    private readonly ISoftPhoneClientNotifier _softPhones;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterOfferAnsweredNotifier"/> class.
    /// </summary>
    /// <param name="realTimeNotifier">The Contact Center real-time notifier.</param>
    /// <param name="softPhones">Pushes to the agent's soft phones.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterOfferAnsweredNotifier(
        IContactCenterRealTimeNotifier realTimeNotifier,
        ISoftPhoneClientNotifier softPhones,
        ILogger<ContactCenterOfferAnsweredNotifier> logger)
    {
        _realTimeNotifier = realTimeNotifier;
        _softPhones = softPhones;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyAnsweredAsync(
        ActivityReservation reservation,
        string agentUserId,
        string providerCallId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        if (string.IsNullOrEmpty(agentUserId))
        {
            return;
        }

        try
        {
            // The soft phones first: they are the ones ringing in the agent's ear.
            await _softPhones.IncomingCallAnsweredAsync(agentUserId, new IncomingCallAnsweredNotification
            {
                CallId = providerCallId,
                OfferId = reservation.ItemId,
            });

            await _realTimeNotifier.NotifyOfferRevokedAsync(new AgentOfferRevokedNotification
            {
                UserId = agentUserId,
                AgentId = reservation.AgentId,
                ReservationId = reservation.ItemId,
                ActivityItemId = reservation.ActivityItemId,
                QueueId = reservation.QueueId,
                Reason = AgentOfferRevokedReason.Accepted,
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best effort: the durable OfferRevoked still follows through the outbox.
            _logger.LogWarning(
                ex,
                "Could not push the answered offer '{ReservationId}' to the agent's open clients.",
                reservation.ItemId.SanitizeLogValue());
        }
    }
}
