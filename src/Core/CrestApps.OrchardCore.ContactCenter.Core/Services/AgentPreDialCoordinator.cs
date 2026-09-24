using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Default <see cref="IAgentPreDialCoordinator"/>.
/// </summary>
/// <remarks>
/// <para>
/// When an agent clicked Answer, the platform used to ring their device, wait for it to answer, and only then join it
/// to the caller: the agent heard nothing for the whole invite-and-answer round trip of their own soft phone, around
/// two seconds of a three-and-a-half-second connect. Ringing the device while the offer is still on screen moves that
/// round trip into the time the agent spends deciding. The agent's client holds the leg without ringing it
/// separately, and answers it the moment they click, in parallel with the accept.
/// </para>
/// <para>
/// Every transition for one offer runs under a lock of its own, and the reservation is always re-read in a fresh scope
/// under it, so a transition never acts on a status another node has since changed. The leg is joined exactly once,
/// by whichever of "accepted and caller ready" and "agent answered" arrives second; a leg whose offer is no longer
/// this agent's to take is hung up and never joined.
/// </para>
/// </remarks>
public sealed class AgentPreDialCoordinator : IAgentPreDialCoordinator
{
    // How long the state outlives the leg's own ring window, so a late webhook still finds the leg it describes
    // (and is recognized as stale) rather than being mistaken for a leg nobody rang.
    private static readonly TimeSpan _stateRetention = TimeSpan.FromMinutes(2);

    private readonly IAgentPreDialLegStore _store;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IContactCenterAgentLegFailureService _agentLegFailureService;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ContactCenterCoordinationOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPreDialCoordinator"/> class.
    /// </summary>
    /// <param name="store">The store holding the pre-dialed legs.</param>
    /// <param name="voiceProviderResolver">The resolver used to reach the provider that owns a call.</param>
    /// <param name="interactionManager">The interaction manager used to resolve the offered call.</param>
    /// <param name="agentManager">The agent profile manager used to resolve the agent's user.</param>
    /// <param name="agentLegFailureService">The service that records an agent leg answering or failing on the call.</param>
    /// <param name="scopeExecutor">The executor used to re-read the reservation in a fresh scope.</param>
    /// <param name="distributedLock">The lock that serializes one offer's transitions.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="options">The coordination options.</param>
    /// <param name="logger">The logger.</param>
    public AgentPreDialCoordinator(
        IAgentPreDialLegStore store,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        IContactCenterAgentLegFailureService agentLegFailureService,
        IContactCenterScopeExecutor scopeExecutor,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<ContactCenterCoordinationOptions> options,
        ILogger<AgentPreDialCoordinator> logger)
    {
        _store = store;
        _voiceProviderResolver = voiceProviderResolver;
        _interactionManager = interactionManager;
        _agentManager = agentManager;
        _agentLegFailureService = agentLegFailureService;
        _scopeExecutor = scopeExecutor;
        _distributedLock = distributedLock;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> PreDialAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (!_options.AgentPreDialEnabled || string.IsNullOrEmpty(reservationId))
        {
            return false;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        if (await _store.FindAsync(reservationId, cancellationToken) is not null)
        {
            // Outbox delivery is at least once. The offer already has its leg.
            return true;
        }

        var reservation = await ReadReservationAsync(reservationId);
        var now = _clock.UtcNow;

        if (reservation is null ||
            reservation.Status != ReservationStatus.Pending ||
            reservation.ExpiresUtc - now < _options.AgentPreDialMinimumOfferRemaining)
        {
            return false;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        // Only a live inbound call that is ringing the agent has a caller waiting to be joined.
        if (interaction is null ||
            interaction.Direction != InteractionDirection.Inbound ||
            interaction.Status != InteractionStatus.Ringing ||
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

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (string.IsNullOrEmpty(agent?.UserId))
        {
            return false;
        }

        // The leg rings for as long as the offer does (plus a little, for an accept made at the last moment), so a
        // leg nobody answers ends with its offer even if nothing here ever hangs it up.
        var ringsUntilUtc = reservation.ExpiresUtc + _options.AgentPreDialRingGrace;
        var timeoutSeconds = (int)Math.Ceiling((ringsUntilUtc - now).TotalSeconds);

        var result = await preDialProvider.PreDialAgentAsync(new ContactCenterAgentPreDialRequest
        {
            ReservationId = reservation.ItemId,
            InteractionId = interaction.ItemId,
            ProviderCallId = interaction.ProviderInteractionId,
            AgentId = reservation.AgentId,
            AgentUserId = agent.UserId,
            TimeoutSeconds = timeoutSeconds,
        }, cancellationToken);

        if (result is null || !result.Succeeded || string.IsNullOrWhiteSpace(result.ProviderLegId))
        {
            // Not an error: the provider or the agent's client cannot hold a leg, and the accept connects the agent
            // the ordinary way.
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The agent for offer '{ReservationId}' was not pre-dialed ({ErrorCode}); the accept will connect them.",
                    reservationId.SanitizeLogValue(),
                    result?.ErrorCode.SanitizeLogValue());
            }

            return false;
        }

        var agentLegId = result.ProviderLegId.Trim();

        // The offer may have ended while the leg was being placed. It may also have been accepted, which is fine: the
        // accept waits on this lock and joins the leg saved below.
        var current = await ReadReservationAsync(reservationId);

        if (current is null ||
            current.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted ||
            !string.Equals(current.AgentId, reservation.AgentId, StringComparison.Ordinal))
        {
            await HangupAsync(preDialProvider, agentLegId, cancellationToken);

            return false;
        }

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
            RingsUntilUtc = ringsUntilUtc,
        }, ringsUntilUtc + _stateRetention, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<AgentPreDialLeg> GetForAcceptAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return null;
        }

        // Taking the lock is what makes an accept wait for a pre-dial still being placed, instead of ringing the
        // agent a second time while the first leg arrives.
        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            _logger.LogWarning(
                "Could not read the pre-dialed leg of offer '{ReservationId}'; the accept will connect the agent directly.",
                reservationId.SanitizeLogValue());

            return null;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        return leg is null || leg.BridgedUtc.HasValue || leg.RingsUntilUtc <= _clock.UtcNow
            ? null
            : leg;
    }

    /// <inheritdoc/>
    public async Task<bool> OnCallerReadyAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return false;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null)
        {
            return false;
        }

        if (leg.BridgedUtc.HasValue)
        {
            return true;
        }

        leg.CallerReadyUtc ??= _clock.UtcNow;

        if (leg.AgentAnsweredUtc.HasValue)
        {
            // The agent's device answered first; the caller being ready is the second half. The topology is left to
            // the answer command that is readying the caller, which records the leg as answered from this result.
            return await BridgeAsync(leg, recordAnswered: false, cancellationToken);
        }

        await SaveAsync(leg, cancellationToken);

        return false;
    }

    /// <inheritdoc/>
    public async Task OnAgentLegAnsweredAsync(string providerName, string reservationId, string agentLegId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(agentLegId))
        {
            return;
        }

        if (string.IsNullOrEmpty(reservationId))
        {
            await HangupAsync(providerName, agentLegId, cancellationToken);

            return;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            // A leg that cannot be decided is never joined. Hanging it up is the safe half of the choice.
            await HangupAsync(providerName, agentLegId, cancellationToken);

            return;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null || !string.Equals(leg.AgentLegId, agentLegId, StringComparison.Ordinal))
        {
            // Nothing is tracking this leg any more: its offer ended and was released. It must not be joined to
            // anyone, and left up it would sit in the agent's ear until it timed out.
            await HangupAsync(providerName, agentLegId, cancellationToken);

            return;
        }

        if (leg.BridgedUtc.HasValue)
        {
            // A redelivered webhook.
            return;
        }

        var reservation = await ReadReservationAsync(reservationId);

        if (reservation is null || !string.Equals(reservation.AgentId, leg.AgentId, StringComparison.Ordinal))
        {
            await ReleaseLegAsync(leg, cancellationToken);

            return;
        }

        switch (reservation.Status)
        {
            case ReservationStatus.Pending:
                // The agent's device answered as the agent clicked, ahead of the accept. The accept joins it.
                leg.AgentAnsweredUtc ??= _clock.UtcNow;
                await SaveAsync(leg, cancellationToken);
                break;

            case ReservationStatus.Accepted:
                leg.AgentAnsweredUtc ??= _clock.UtcNow;

                if (leg.CallerReadyUtc.HasValue)
                {
                    await BridgeAsync(leg, recordAnswered: true, cancellationToken);
                }
                else
                {
                    await SaveAsync(leg, cancellationToken);
                }

                break;

            default:
                // Declined, expired, revoked or re-offered: this leg belongs to an offer the agent no longer holds.
                await ReleaseLegAsync(leg, cancellationToken);
                break;
        }
    }

    /// <inheritdoc/>
    public async Task OnAgentLegEndedAsync(
        string providerName,
        string reservationId,
        string agentLegId,
        HangupCause? cause,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId) || string.IsNullOrEmpty(agentLegId))
        {
            return;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null || !string.Equals(leg.AgentLegId, agentLegId, StringComparison.Ordinal))
        {
            return;
        }

        await _store.RemoveAsync(leg, cancellationToken);

        if (leg.BridgedUtc.HasValue)
        {
            // The agent's leg of a conversation that took place; its end is the call's own business.
            return;
        }

        var reservation = await ReadReservationAsync(reservationId);

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
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return;
        }

        await using var acquiredLock = locker;

        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null || leg.BridgedUtc.HasValue)
        {
            return;
        }

        await ReleaseLegAsync(leg, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReleaseForInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        var reservationId = await _store.FindReservationIdByInteractionAsync(interactionId, cancellationToken);

        await ReleaseAsync(reservationId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReleaseForAgentAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var reservationId = await _store.FindReservationIdByAgentAsync(agentId, cancellationToken);

        await ReleaseAsync(reservationId, cancellationToken);
    }

    private async Task<bool> BridgeAsync(AgentPreDialLeg leg, bool recordAnswered, CancellationToken cancellationToken)
    {
        if (_voiceProviderResolver.Get(leg.ProviderName) is not IContactCenterVoiceAgentPreDialProvider provider)
        {
            await _store.RemoveAsync(leg, cancellationToken);

            return false;
        }

        // Claimed before the provider is asked, so a second trigger arriving while the bridge is in flight finds the
        // leg joined and does nothing.
        leg.BridgedUtc = _clock.UtcNow;
        await SaveAsync(leg, cancellationToken);

        var result = await provider.BridgePreDialedAgentAsync(leg.ProviderCallId, leg.AgentLegId, cancellationToken);

        if (result?.Succeeded == true)
        {
            if (recordAnswered)
            {
                // The leg's own answered event is keyed by the agent leg, which belongs to no interaction. Record the
                // answer against the caller's call so the topology says the agent connected.
                await _agentLegFailureService.RecordAnsweredAsync(
                    leg.ProviderName,
                    peerProviderCallId: leg.ProviderCallId,
                    agentLegProviderCallId: leg.AgentLegId,
                    cancellationToken);
            }

            return true;
        }

        _logger.LogWarning(
            "The provider refused to join the pre-dialed agent leg of offer '{ReservationId}' to the caller ({ErrorCode}).",
            leg.ReservationId.SanitizeLogValue(),
            result?.ErrorCode.SanitizeLogValue());

        await HangupAsync(provider, leg.AgentLegId, cancellationToken);
        await _store.RemoveAsync(leg, cancellationToken);
        await _agentLegFailureService.FailAsync(leg.ProviderName, leg.ProviderCallId, HangupCause.Failed, cancellationToken);

        return false;
    }

    private async Task ReleaseLegAsync(AgentPreDialLeg leg, CancellationToken cancellationToken)
    {
        await _store.RemoveAsync(leg, cancellationToken);
        await HangupAsync(leg.ProviderName, leg.AgentLegId, cancellationToken);
    }

    private Task SaveAsync(AgentPreDialLeg leg, CancellationToken cancellationToken)
        => _store.SaveAsync(leg, leg.RingsUntilUtc + _stateRetention, cancellationToken);

    private Task HangupAsync(string providerName, string agentLegId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerName) ||
            _voiceProviderResolver.Get(providerName) is not IContactCenterVoiceAgentPreDialProvider provider)
        {
            return Task.CompletedTask;
        }

        return HangupAsync(provider, agentLegId, cancellationToken);
    }

    private async Task HangupAsync(IContactCenterVoiceAgentPreDialProvider provider, string agentLegId, CancellationToken cancellationToken)
    {
        try
        {
            await provider.HangupPreDialedAgentAsync(agentLegId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The leg still ends on its own when its ring window runs out.
            _logger.LogWarning(ex, "Could not hang up the pre-dialed agent leg '{AgentLegId}'.", agentLegId.SanitizeLogValue());
        }
    }

    private async Task<ActivityReservation> ReadReservationAsync(string reservationId)
    {
        ActivityReservation reservation = null;

        // A fresh scope, so the status read is the committed one rather than whatever this scope loaded earlier.
        await _scopeExecutor.ExecuteAsync<IActivityReservationManager>(async manager =>
        {
            reservation = await manager.FindByIdAsync(reservationId);
        });

        return reservation;
    }

    private Task<(ILocker Locker, bool Locked)> AcquireAsync(string reservationId)
        => _distributedLock.TryAcquireLockAsync(
            $"ContactCenterAgentPreDial:{reservationId}",
            _options.ReservationLockTimeout,
            _options.ReservationLockExpiration);
}
