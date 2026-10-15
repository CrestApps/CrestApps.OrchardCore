using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The agent leg rung for a call the dialer placed for an agent who already holds its reservation: rung while the
/// customer rings, answered by the agent's phone on arrival, and parked until the customer answers.
/// </summary>
public sealed partial class AgentPreDialCoordinator
{
    // How long a parked leg rings before the provider gives up on it. The agent's device answers it on arrival, so this
    // only bounds a device that never does; the answer then rings the agent the ordinary way.
    private const int ParkedLegRingSeconds = 30;

    /// <inheritdoc/>
    public async Task<bool> ParkForDialedCallAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        if (!_options.DialerAgentParkingEnabled || string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);
        var reservationId = interaction is null ? null : DialerCallMetadata.GetReservationId(interaction);

        // Only a call the dialer placed for an agent already holding its reservation, whose customer is still ringing.
        // An over-dialed call has no agent until a person answers, and a call already answered connects its agent now.
        if (string.IsNullOrEmpty(reservationId) ||
            !DialerCallMetadata.IsCampaignDial(interaction) ||
            DialerCallMetadata.IsOverDialed(interaction) ||
            string.IsNullOrEmpty(interaction.AgentId) ||
            interaction.Status is not InteractionStatus.Created and not InteractionStatus.Ringing ||
            interaction.AnsweredUtc.HasValue ||
            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId) ||
            string.IsNullOrWhiteSpace(interaction.ProviderName))
        {
            return false;
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (provider is not IContactCenterVoiceAgentPreDialProvider preDialProvider ||
            provider.DeliveryModel != VoiceProviderDeliveryModel.ServerSideAcd)
        {
            return false;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        if (await _store.FindAsync(reservationId, cancellationToken) is { } existing)
        {
            // Already parked (a redelivered trigger), or the customer answered first and the answer went ahead without a
            // parked leg: ringing the agent now would put a second leg in their ear.
            return !string.IsNullOrEmpty(existing.AgentLegId);
        }

        var reservation = await ReadReservationAsync(reservationId);

        if (reservation is null ||
            reservation.Status != ReservationStatus.Accepted ||
            !string.Equals(reservation.AgentId, interaction.AgentId, StringComparison.Ordinal))
        {
            return false;
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (string.IsNullOrEmpty(agent?.UserId))
        {
            return false;
        }

        var result = await preDialProvider.PreDialAgentAsync(new ContactCenterAgentPreDialRequest
        {
            ReservationId = reservation.ItemId,
            InteractionId = interaction.ItemId,
            ProviderCallId = interaction.ProviderInteractionId,
            AgentId = reservation.AgentId,
            AgentUserId = agent.UserId,
            TimeoutSeconds = ParkedLegRingSeconds,
            AnswerOnArrival = true,
        }, cancellationToken);

        if (result is null || !result.Succeeded || string.IsNullOrWhiteSpace(result.ProviderLegId))
        {
            // Not an error: the answer rings the agent the ordinary way.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The agent of dialed call '{InteractionId}' was not parked ({ErrorCode}); the answer will ring them.",
                    interactionId.SanitizeLogValue(),
                    result?.ErrorCode.SanitizeLogValue());
            }

            return false;
        }

        var agentLegId = result.ProviderLegId.Trim();

        // The customer may have hung up, or the call been given up on, while the leg was being placed. Nothing would then
        // ever join or release the leg, so it is hung up now.
        if (await IsCallOverAsync(interactionId))
        {
            await HangupAsync(preDialProvider, agentLegId, cancellationToken);

            return false;
        }

        var now = _clock.UtcNow;

        await _store.SaveAsync(new AgentPreDialLeg
        {
            ReservationId = reservation.ItemId,
            AgentId = reservation.AgentId,
            AgentUserId = agent.UserId,
            InteractionId = interaction.ItemId,
            ProviderName = provider.TechnicalName,
            ProviderCallId = interaction.ProviderInteractionId,
            AgentLegId = agentLegId,
            DialedUtc = now,
            RingsUntilUtc = now + _options.DialerAgentParkWindow,
            ParkedForDialedCall = true,
        }, now + _options.DialerAgentParkWindow + _stateRetention, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Parked agent leg '{AgentLegId}' for dialed call '{ProviderCallId}' (interaction '{InteractionId}') while the customer rings; the answer joins the two.",
                agentLegId.SanitizeLogValue(),
                interaction.ProviderInteractionId.SanitizeLogValue(),
                interactionId.SanitizeLogValue());
        }

        return true;
    }

    /// <inheritdoc/>
    public async Task<AgentPreDialLeg> TakeParkedLegAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return null;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            _logger.LogWarning(
                "Could not read the parked agent leg of reservation '{ReservationId}'; the answer will ring the agent.",
                reservationId.SanitizeLogValue());

            return null;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);
        var now = _clock.UtcNow;

        if (leg is null)
        {
            // The answer goes ahead without a parked leg. Recorded, so a park that has not run yet finds the answer
            // already made and does not ring the agent a second time.
            if (_options.DialerAgentParkingEnabled)
            {
                await _store.SaveAsync(new AgentPreDialLeg
                {
                    ReservationId = reservationId,
                    ParkedForDialedCall = true,
                    CallerReadyUtc = now,
                    RingsUntilUtc = now + _options.DialerAgentParkWindow,
                }, now + _options.DialerAgentParkWindow + _stateRetention, cancellationToken);
            }

            return null;
        }

        if (!leg.ParkedForDialedCall || string.IsNullOrEmpty(leg.AgentLegId) || leg.BridgedUtc.HasValue)
        {
            return null;
        }

        if (leg.RingsUntilUtc <= now)
        {
            // Parked longer than any customer rings: whatever it waited for is long gone.
            await ReleaseLegAsync(leg, cancellationToken);

            return null;
        }

        return leg;
    }

    // Read in a fresh scope, so a call the customer just hung up on is seen as over.
    private async Task<bool> IsCallOverAsync(string interactionId)
    {
        var over = false;

        await _scopeExecutor.ExecuteAsync<IInteractionManager>(async manager =>
        {
            var interaction = await manager.FindByIdAsync(interactionId);

            over = interaction is null || interaction.Status is InteractionStatus.Ended or InteractionStatus.Failed;
        });

        return over;
    }
}
