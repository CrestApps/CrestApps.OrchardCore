using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The end of a pre-dialed agent leg before it was joined: forgotten, and the offer it was to carry settled.
/// </summary>
public sealed partial class AgentPreDialCoordinator
{
    /// <inheritdoc/>
    public Task OnAgentLegEndedAsync(
        string providerName,
        string reservationId,
        string agentLegId,
        HangupCause? cause,
        CancellationToken cancellationToken = default)
        => EndAgentLegAsync(reservationId, agentLegId, cause, phoneUnreachable: false, cancellationToken);

    /// <inheritdoc/>
    public Task OnAgentPhoneUnreachableAsync(
        string providerName,
        string reservationId,
        string agentLegId,
        HangupCause? cause,
        CancellationToken cancellationToken = default)
        => EndAgentLegAsync(reservationId, agentLegId, cause, phoneUnreachable: true, cancellationToken);

    private async Task EndAgentLegAsync(
        string reservationId,
        string agentLegId,
        HangupCause? cause,
        bool phoneUnreachable,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reservationId) || string.IsNullOrEmpty(agentLegId))
        {
            return;
        }

        var unreachableAgentId = await ForgetAgentLegAsync(reservationId, agentLegId, cause, phoneUnreachable, cancellationToken);

        // Released only once the offer's lock is let go: the release publishes the agent's release, whose handler asks
        // this coordinator to release the same offer under that lock, and would otherwise wait out its timeout behind the
        // caller holding it. The leg is already forgotten, so that release finds nothing left to do.
        if (unreachableAgentId is not null)
        {
            await ReleaseUnreachableOfferAsync(reservationId, unreachableAgentId, cancellationToken);
        }
    }

    // Forgets the ended leg under the offer's lock and settles what it leaves behind. Returns the agent whose ringing offer
    // is to be released because their phone refused its leg, or null when there is nothing more to do.
    private async Task<string> ForgetAgentLegAsync(
        string reservationId,
        string agentLegId,
        HangupCause? cause,
        bool phoneUnreachable,
        CancellationToken cancellationToken)
    {

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null || !string.Equals(leg.AgentLegId, agentLegId, StringComparison.Ordinal))
        {
            return null;
        }

        await _store.RemoveAsync(leg, cancellationToken);

        if (leg.BridgedUtc.HasValue)
        {
            // The agent's leg of a conversation that took place; its end is the call's own business.
            return null;
        }

        if (leg.ParkedForDialedCall && !leg.CallerReadyUtc.HasValue)
        {
            // A parked leg is only a head start. Lost before the customer answered, it fails nothing: the answer finds no
            // parked leg and rings the agent the ordinary way.
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The parked agent leg '{AgentLegId}' of reservation '{ReservationId}' ended before the customer answered ({HangupCause}); the answer will ring the agent.",
                    agentLegId.SanitizeLogValue(),
                    reservationId.SanitizeLogValue(),
                    cause);
            }

            return null;
        }

        var reservation = await ReadReservationAsync(reservationId);

        if (phoneUnreachable &&
            reservation?.Status == ReservationStatus.Pending &&
            string.Equals(reservation.AgentId, leg.AgentId, StringComparison.Ordinal))
        {
            // The offer is still on the agent's screen, but no phone of theirs is there to take it: the leg was refused
            // as unavailable and the provider had nowhere else to ring. Left alone, the caller would hear nothing until
            // the ring window ran out. The offer is released now and the caller handed on.
            return leg.AgentId;
        }

        if (reservation?.Status == ReservationStatus.Accepted &&
            string.Equals(reservation.AgentId, leg.AgentId, StringComparison.Ordinal))
        {
            // The agent accepted, and the leg that was to carry them to the caller is gone before it was joined. This
            // is the same failure as an agent leg that could not be reached, and ends the same way.
            _logger.LogWarning(
                "The pre-dialed agent leg of accepted offer '{ReservationId}' ended before it was joined to the caller.",
                reservationId.SanitizeLogValue());

            await _agentLegFailureService.FailAsync(
                leg.ProviderName,
                leg.ProviderCallId,
                cause ?? HangupCause.NoAnswer,
                cancellationToken);
        }

        return null;
    }

    private async Task ReleaseUnreachableOfferAsync(string reservationId, string agentId, CancellationToken cancellationToken)
    {
        try
        {
            // A fresh scope, as the reservation's own transitions run: the release commits its unit of work.
            await _scopeExecutor.ExecuteAsync<IActivityReservationService>(service =>
                service.ReleaseUnreachableOfferAsync(reservationId, agentId, cancellationToken));
        }
        catch (ConcurrencyException)
        {
            // An accept, a decline or the ring window's own deadline settled the offer first, and decided it.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Another transition settled offer '{ReservationId}' before it could be released for agent '{AgentId}', whose phone refused it.",
                    reservationId.SanitizeLogValue(),
                    agentId.SanitizeLogValue());
            }
        }
    }
}
