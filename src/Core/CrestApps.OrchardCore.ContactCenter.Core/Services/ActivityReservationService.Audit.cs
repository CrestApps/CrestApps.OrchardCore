using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// How an offer the platform settled on its own is written to the audit log: one that rang out, and one that was
/// taken back before anybody answered.
/// </summary>
public sealed partial class ActivityReservationService
{
    /// <summary>
    /// Records the settlement of an offer that expired or was cancelled. A decline and an acceptance are the
    /// agent's own acts and are recorded where the agent made them.
    /// </summary>
    private async Task RecordOfferSettledAsync(
        ActivityReservation reservation,
        Interaction interaction,
        AgentProfile agent,
        DateTime settledUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var eventType = reservation.Status switch
        {
            ReservationStatus.Expired => ContactCenterConstants.Events.OfferExpired,
            ReservationStatus.Canceled => ContactCenterConstants.Events.OfferCancelled,
            _ => null,
        };

        if (eventType is null)
        {
            return;
        }

        interaction ??= await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        // Work that never had an interaction -- a preview dial that has not been placed -- was never offered as a
        // call, so there is nothing ringing to report on.
        if (interaction is null)
        {
            return;
        }

        await _auditRecorder.RecordOfferAsync(
            eventType,
            ContactCenterCallAudit.ForOffer(reservation, interaction, agent, settledUtc, reason),
            ContactCenterActor.System,
            cancellationToken);
    }
}
