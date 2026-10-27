using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

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
public sealed partial class AgentPreDialCoordinator : IAgentPreDialCoordinator
{
    // How long the state outlives the leg's own ring window, so a late webhook still finds the leg it describes
    // (and is recognized as stale) rather than being mistaken for a leg nobody rang.
    private static readonly TimeSpan _stateRetention = TimeSpan.FromMinutes(2);

    // A join races a few webhook writes of the same call; more attempts than that is a loop.
    private const int MaxJoinRecordAttempts = 4;

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

        AgentPreDialLeg joinedLeg;

        await using (locker)
        {
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

            if (!leg.AgentAnsweredUtc.HasValue)
            {
                await SaveAsync(leg, cancellationToken);

                return false;
            }

            // The agent's device answered first; the caller being ready is the second half.
            if (!await BridgeAsync(leg, cancellationToken))
            {
                return false;
            }

            joinedLeg = leg;
        }

        // This runs inside the answer command that readied the caller, so the join is committed in a scope of its own, and
        // only once the offer's lock is released (see RecordJoinedAsync).
        await RecordJoinedAsync(joinedLeg, inOwnScope: true);

        return true;
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

        AgentPreDialLeg joinedLeg = null;

        await using (locker)
        {
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
                // A redelivered webhook -- or the retry of one whose work failed after the bridge, which is marked before
                // the join is recorded and outside the work's own transaction (a concurrent update of the call refused its
                // save). Returning left the agent talking to the customer while still shown "Waiting for a connected
                // call". The join is recorded again, which changes nothing when it already was.
                joinedLeg = leg;
            }
            else
            {
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
                            if (await BridgeAsync(leg, cancellationToken))
                            {
                                joinedLeg = leg;
                            }
                        }
                        else
                        {
                            // Also a parked leg the agent's device answered on arrival: it waits for the customer to answer.
                            await SaveAsync(leg, cancellationToken);
                        }

                        break;

                    default:
                        // Declined, expired, revoked or re-offered: this leg belongs to an offer the agent no longer holds.
                        await ReleaseLegAsync(leg, cancellationToken);
                        break;
                }
            }
        }

        if (joinedLeg is not null)
        {
            await RecordJoinedAsync(joinedLeg, inOwnScope: false);
        }
    }

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

    /// <inheritdoc/>
    public async Task<bool> RedialAgentLegAsync(
        string providerName,
        string reservationId,
        string failedAgentLegId,
        string agentEndpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(reservationId) || string.IsNullOrEmpty(failedAgentLegId) || string.IsNullOrWhiteSpace(agentEndpoint))
        {
            return false;
        }

        (var locker, var locked) = await AcquireAsync(reservationId);

        if (!locked)
        {
            return false;
        }

        await using var acquiredLock = locker;

        // Placed under the offer's lock, so the new leg's answer waits here and finds the leg it belongs to.
        var leg = await _store.FindAsync(reservationId, cancellationToken);

        if (leg is null ||
            leg.BridgedUtc.HasValue ||
            !string.Equals(leg.AgentLegId, failedAgentLegId, StringComparison.Ordinal) ||
            (!string.IsNullOrEmpty(providerName) && !string.Equals(leg.ProviderName, providerName, StringComparison.Ordinal)) ||
            _voiceProviderResolver.Get(leg.ProviderName) is not IContactCenterVoiceAgentPreDialProvider provider)
        {
            return false;
        }

        var reservation = await ReadReservationAsync(reservationId);
        var remaining = leg.RingsUntilUtc - _clock.UtcNow;

        // Telnyx rings a leg for five seconds at the least; an offer with less left than that is not worth a second leg.
        if (reservation is null ||
            reservation.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted ||
            !string.Equals(reservation.AgentId, leg.AgentId, StringComparison.Ordinal) ||
            remaining < TimeSpan.FromSeconds(5))
        {
            return false;
        }

        var result = await provider.PreDialAgentAsync(new ContactCenterAgentPreDialRequest
        {
            ReservationId = leg.ReservationId,
            InteractionId = leg.InteractionId,
            ProviderCallId = leg.ProviderCallId,
            AgentId = leg.AgentId,
            AgentUserId = leg.AgentUserId,
            TimeoutSeconds = leg.ParkedForDialedCall
                ? ParkedLegRingSeconds
                : (int)Math.Ceiling(remaining.TotalSeconds),
            AgentEndpoint = agentEndpoint,
            ReplacesAgentLegId = failedAgentLegId,
            AnswerOnArrival = leg.ParkedForDialedCall,
        }, cancellationToken);

        if (result is null || !result.Succeeded || string.IsNullOrWhiteSpace(result.ProviderLegId))
        {
            return false;
        }

        leg.AgentLegId = result.ProviderLegId.Trim();
        leg.AgentAnsweredUtc = null;
        await SaveAsync(leg, cancellationToken);

        return true;
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

    // Joins the two; the caller records the join once it has released the offer's lock (see RecordJoinedAsync).
    private async Task<bool> BridgeAsync(AgentPreDialLeg leg, CancellationToken cancellationToken)
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

        // Never cancelled once asked for: inside the answer command, its timeout cancels the work while the provider may
        // already be joining the two, and a bridge abandoned halfway was never recorded.
        var result = await provider.BridgePreDialedAgentAsync(leg.ProviderCallId, leg.AgentLegId, CancellationToken.None);

        if (result?.Succeeded == true)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Joined pre-dialed agent leg '{AgentLegId}' of reservation '{ReservationId}' to call '{ProviderCallId}'.",
                    leg.AgentLegId.SanitizeLogValue(),
                    leg.ReservationId.SanitizeLogValue(),
                    leg.ProviderCallId.SanitizeLogValue());
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

    // The agent and the customer are talking from the moment the bridge succeeds, so that is when the join is recorded:
    // the agent goes on the call, and their screens show it and open its record. The leg's own answered event is keyed by
    // the agent leg, which belongs to no interaction, so the answer is recorded against the caller's call.
    //
    // Recorded here whichever half arrived second. When it was the customer answering, the join was left to the answer
    // command that readied the caller, which recorded it only once its own result was projected: a command that ran past
    // its timeout (waiting on this offer's lock counts against it), or whose projection failed, left the two talking with
    // the agent still Reserved, shown "Waiting for a connected call" and no record. Inside that command nothing would
    // commit it, so it is committed in a scope of its own; it is never cancelled, as the bridge cannot be taken back.
    // Recording it again, as the command's projection or a redelivered answer then does, changes nothing.
    //
    // Never recorded while holding the offer's lock. The call's answer webhook writes the same call in its transaction and
    // then waits on that lock to take the parked leg, so recording under it waited on the webhook's row locks while the
    // webhook waited on the lock: both stalled until the lock timed out, the join lost its save, and by then the answer
    // command's own timeout had abandoned it, leaving the agent Reserved with no record until the customer hung up.
    private async Task RecordJoinedAsync(AgentPreDialLeg leg, bool inOwnScope)
    {
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (inOwnScope)
                    {
                        await _scopeExecutor.ExecuteAsync<IContactCenterAgentLegFailureService>(service =>
                            RecordLegAnsweredAsync(service, leg));
                    }
                    else
                    {
                        await RecordLegAnsweredAsync(_agentLegFailureService, leg);
                    }

                    break;
                }
                catch (ConcurrencyException) when (attempt < MaxJoinRecordAttempts)
                {
                    // The call's webhooks (the customer's answer, the bridge) write the same call session at the moment
                    // of the join. Losing that race used to leave the agent talking to the customer while still shown
                    // "Waiting for a connected call", with no activity to work. The join is recorded again on a fresh
                    // read, in a scope of its own: a failed flush has already cancelled the caller's session.
                    inOwnScope = true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "The pre-dialed agent leg '{AgentLegId}' of reservation '{ReservationId}' was joined to call '{ProviderCallId}', but the join could not be recorded.",
                leg.AgentLegId.SanitizeLogValue(),
                leg.ReservationId.SanitizeLogValue(),
                leg.ProviderCallId.SanitizeLogValue());
        }
    }

    private static Task<bool> RecordLegAnsweredAsync(IContactCenterAgentLegFailureService service, AgentPreDialLeg leg)
        => service.RecordAnsweredAsync(
            leg.ProviderName,
            peerProviderCallId: leg.ProviderCallId,
            agentLegProviderCallId: leg.AgentLegId,
            CancellationToken.None);

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
