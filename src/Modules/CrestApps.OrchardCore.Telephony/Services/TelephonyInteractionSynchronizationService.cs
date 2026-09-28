using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR.Core;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Reconciles locally persisted telephony interactions with provider-authoritative call state.
/// </summary>
public sealed class TelephonyInteractionSynchronizationService : ITelephonyInteractionSynchronizationService
{
    private const string ReconciliationLockKey = "TelephonyInteractionStateReconciliation";
    private const int MaxReconciliationBatchSize = 200;
    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly string _tenantName;
    private readonly TimeSpan _lockTimeout;
    private readonly TimeSpan _lockExpiration;
    private readonly TimeSpan _newInteractionGracePeriod;
    private readonly TimeSpan _clientRecordedCallMaxAge;
    private readonly TimeSpan _clientRecordedCallSilenceTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyInteractionSynchronizationService"/> class.
    /// </summary>
    /// <param name="interactionStore">The telephony interaction store.</param>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="hubContext">The soft-phone hub context.</param>
    /// <param name="distributedLock">The distributed lock used to prevent overlapping reconciliation sweeps.</param>
    /// <param name="clock">The clock used to stamp terminal interactions.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    /// <param name="coordinationOptions">The distributed-lock timings this deployment coordinates with.</param>
    public TelephonyInteractionSynchronizationService(
        ITelephonyInteractionStore interactionStore,
        ITelephonyProviderResolver providerResolver,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<TelephonyInteractionSynchronizationService> logger,
        ShellSettings shellSettings,
        IOptions<TelephonyCoordinationOptions> coordinationOptions)
    {
        _interactionStore = interactionStore;
        _providerResolver = providerResolver;
        _hubContext = hubContext;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
        _tenantName = shellSettings.Name;
        _lockTimeout = coordinationOptions.Value.InteractionLockTimeout;
        _lockExpiration = coordinationOptions.Value.InteractionLockExpiration;
        _newInteractionGracePeriod = coordinationOptions.Value.NewInteractionGracePeriod;
        _clientRecordedCallMaxAge = coordinationOptions.Value.ClientRecordedCallMaxAge;
        _clientRecordedCallSilenceTimeout = coordinationOptions.Value.ClientRecordedCallSilenceTimeout;
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallLookupResult> GetActiveCallAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var interaction = await _interactionStore.FindActiveByUserAsync(userId, cancellationToken);

        if (interaction is null)
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            };
        }

        var (lookup, _) = await RefreshInteractionAsync(
            interaction,
            notifyOrphanRemoval: true,
            notifyProviderState: false,
            cancellationToken);

        return lookup;
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallListLookupResult> GetActiveCallsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var interactions = await _interactionStore.GetActiveByUserAsync(userId, cancellationToken);
        var calls = new List<TelephonyCall>(interactions.Count);

        foreach (var interaction in interactions)
        {
            var (lookup, _) = await RefreshInteractionAsync(
                interaction,
                notifyOrphanRemoval: true,
                notifyProviderState: false,
                cancellationToken);

            if (!lookup.Succeeded)
            {
                return new TelephonyCallListLookupResult
                {
                    Succeeded = false,
                    Error = lookup.Error,
                };
            }

            if (lookup.Found && lookup.Call is not null)
            {
                calls.Add(lookup.Call);
            }
        }

        return new TelephonyCallListLookupResult
        {
            Succeeded = true,
            Calls = calls,
        };
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default)
    {
        return await ReconcileAsync(providerName: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileProviderInteractionsAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        return await ReconcileAsync(providerName, cancellationToken);
    }

    private async Task<int> ReconcileAsync(string providerName, CancellationToken cancellationToken)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            ReconciliationLockKey,
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            return 0;
        }

        await using var acquiredLock = locker;

        var interactions = string.IsNullOrEmpty(providerName)
            ? await _interactionStore.GetActiveAsync(MaxReconciliationBatchSize, cancellationToken)
            : await _interactionStore.GetActiveAsync(providerName, MaxReconciliationBatchSize, cancellationToken);
        var changed = 0;

        foreach (var interaction in interactions)
        {
            var (_, interactionChanged) = await RefreshInteractionAsync(
                interaction,
                notifyOrphanRemoval: true,
                notifyProviderState: true,
                cancellationToken);

            if (interactionChanged)
            {
                changed++;
            }
        }

        return changed;
    }

    private async Task<(TelephonyCallLookupResult Lookup, bool Changed)> RefreshInteractionAsync(
        TelephonyInteraction interaction,
        bool notifyOrphanRemoval,
        bool notifyProviderState,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(interaction.CallId))
        {
            await RemoveOrphanAsync(
                interaction,
                notifyOrphanRemoval,
                "the interaction does not contain a call identifier",
                cancellationToken);

            return (new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            }, true);
        }

        // An interaction with a call identifier but no provider identity is one the client recorded itself: a
        // browser-originated call the provider SDK placed directly, which the platform never saw and cannot look
        // up. There is nothing here to reconcile against, and the client settles it when the call ends. Treating
        // it as an orphan announced a terminal state to the soft phone that was still on the call -- which then
        // hung up its own live session -- on the first sweep after the call passed the minute mark. The only case
        // left for the sweep is a browser that vanished mid-call and never reported the end. The phone reports each
        // call it still has up, so one it has stopped reporting is settled as of the last report (see
        // ClientRecordedCallPolicy); one past the maximum age is removed. Both quietly: there is no live phone left to
        // tell, and a late announcement could only reach a phone that has since started another call.
        if (string.IsNullOrWhiteSpace(interaction.ProviderName))
        {
            if (interaction.StartedUtc != default &&
                _clock.UtcNow - interaction.StartedUtc > _clientRecordedCallMaxAge)
            {
                await RemoveOrphanAsync(
                    interaction,
                    notifyUser: false,
                    "a client-recorded call exceeded the maximum age without the client reporting its end",
                    cancellationToken);

                return (new TelephonyCallLookupResult
                {
                    Succeeded = true,
                    Found = false,
                }, true);
            }

            var settled = ClientRecordedCallPolicy.HasGoneSilent(interaction, _clock.UtcNow, _clientRecordedCallSilenceTimeout) &&
                await SettleUnreportedAsync(interaction, cancellationToken);

            return (new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            }, settled);
        }

        var provider = await _providerResolver.GetAsync(interaction.ProviderName);

        if (provider is null)
        {
            // The interaction is correlated under a canonical provider identity, so the exact technical name
            // may not resolve when the call was placed through the configuration-backed default provider (whose
            // registered name differs from the canonical identity). Fall back to the tenant's default provider,
            // which owns the shared connection, before treating the interaction as orphaned.
            provider = await _providerResolver.GetAsync();
        }

        if (provider is null)
        {
            await RemoveOrphanAsync(
                interaction,
                notifyOrphanRemoval,
                "the interaction's provider is no longer registered or enabled",
                cancellationToken);

            return (new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            }, true);
        }

        if (provider is not ITelephonyCallStateProvider stateProvider)
        {
            var error = $"Provider '{interaction.ProviderName}' does not support authoritative call-state lookup.";

            _logger.LogWarning(
                "Unable to reconcile telephony interaction {InteractionId}: {ErrorMessage}",
                interaction.InteractionId.SanitizeLogValue(),
                error.SanitizeLogValue());

            return (new TelephonyCallLookupResult
            {
                Succeeded = false,
                Found = false,
                Error = error,
            }, false);
        }

        var lookup = await stateProvider.GetCallStateAsync(interaction.CallId, cancellationToken);

        if (!lookup.Succeeded)
        {
            _logger.LogWarning(
                "Unable to reconcile telephony interaction {InteractionId} with provider {ProviderName} call {CallId}: {ErrorMessage}",
                interaction.InteractionId.SanitizeLogValue(),
                interaction.ProviderName,
                interaction.CallId.SanitizeLogValue(),
                lookup.Error.SanitizeLogValue());

            return (lookup, false);
        }

        if (!lookup.Found)
        {
            if (interaction.StartedUtc != default &&
                _clock.UtcNow - interaction.StartedUtc < _newInteractionGracePeriod)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Deferred removal of new telephony interaction {InteractionId} for provider {ProviderName} call {CallId} while the provider propagates the originated call.",
                        interaction.InteractionId.SanitizeLogValue(),
                        interaction.ProviderName,
                        interaction.CallId.SanitizeLogValue());
                }

                return (lookup, false);
            }

            await RemoveOrphanAsync(
                interaction,
                notifyOrphanRemoval,
                "the provider no longer reports the call",
                cancellationToken);

            return (lookup, true);
        }

        if (lookup.Call is null)
        {
            var error = $"Provider '{interaction.ProviderName}' returned an empty call state for '{interaction.CallId}'.";

            _logger.LogWarning(
                "Unable to reconcile telephony interaction {InteractionId}: {ErrorMessage}",
                interaction.InteractionId.SanitizeLogValue(),
                error.SanitizeLogValue());

            return (new TelephonyCallLookupResult
            {
                Succeeded = false,
                Found = true,
                Error = error,
            }, false);
        }

        var call = NormalizeCall(interaction, lookup.Call);

        // The lookup is of the call, not of this user's part in it. A caller waiting for an agent is on a leg the
        // platform answered itself, and a provider that can only say a call is alive reports it as connected; handed
        // to the phone still ringing for it, that showed a call nobody had answered as in progress. Until the user
        // joins the call, a live call is ringing them. The reported object is the lookup's own, so the result says it.
        if (interaction.AwaitingAnswer && call.State is CallState.Connecting or CallState.Connected or CallState.OnHold)
        {
            call.State = CallState.Ringing;
            call.IsOnHold = false;
        }

        var changed = false;

        // The mutation runs against the version the store reads inside its own retry scope, so a provider snapshot
        // can never overwrite a newer state that a real-time event committed while this reconciliation pass ran.
        await _interactionStore.UpdateByIdAsync(
            interaction.InteractionId,
            candidate =>
            {
                changed = ApplyProviderState(candidate, call);

                return changed;
            },
            cancellationToken);

        // Only the end of a call is announced. The lookup says whether a call still exists; how a live one stands --
        // ringing an agent, on hold, talking -- is not something every provider can tell (some report any live call as
        // connected), and the real-time events have already told the phone. Announcing it told a phone ringing for an
        // offer that the caller's live leg was a connected call: the prompt vanished for a call nobody had accepted.
        if (notifyProviderState && call.State is CallState.Disconnected or CallState.Failed)
        {
            await NotifyUserAsync(interaction.UserId, call);
        }

        return (lookup, changed);
    }

    // Settles a client-recorded call the soft phone stopped reporting. The decision is taken again against the version
    // the store reads inside its retry scope, so a report that landed after the sweep read the call keeps it alive.
    private async Task<bool> SettleUnreportedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var settled = false;

        await _interactionStore.UpdateByIdAsync(
            interaction.InteractionId,
            candidate => settled = ClientRecordedCallPolicy.HasGoneSilent(candidate, utcNow, _clientRecordedCallSilenceTimeout) &&
                ClientRecordedCallPolicy.SettleUnreported(candidate),
            cancellationToken);

        if (settled && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Settled client-recorded telephony interaction {InteractionId} for call {CallId}: the soft phone stopped reporting it more than {SilenceTimeout} ago.",
                interaction.InteractionId.SanitizeLogValue(),
                interaction.CallId.SanitizeLogValue(),
                _clientRecordedCallSilenceTimeout);
        }

        return settled;
    }

    private async Task RemoveOrphanAsync(
        TelephonyInteraction interaction,
        bool notifyUser,
        string reason,
        CancellationToken cancellationToken)
    {
        await _interactionStore.DeleteAsync(interaction, cancellationToken);

        var disconnectedCall = new TelephonyCall
        {
            CallId = interaction.CallId,
            From = interaction.From,
            To = interaction.To,
            State = CallState.Disconnected,
            Direction = interaction.Direction,
            ProviderName = interaction.ProviderName,
            StartedUtc = interaction.StartedUtc == default
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(interaction.StartedUtc, DateTimeKind.Utc)),
        };

        if (notifyUser)
        {
            await NotifyUserAsync(interaction.UserId, disconnectedCall);
        }

        _logger.LogWarning(
            "Removed orphaned in-progress telephony interaction {InteractionId} for provider {ProviderName} call {CallId} because {Reason}.",
            interaction.InteractionId.SanitizeLogValue(),
            interaction.ProviderName,
            interaction.CallId.SanitizeLogValue(),
            reason);
    }

    private bool ApplyProviderState(TelephonyInteraction interaction, TelephonyCall call)
    {
        var changed = false;

        changed |= SetIfDifferent(interaction.ProviderName, call.ProviderName, value => interaction.ProviderName = value);
        changed |= SetIfDifferent(interaction.From, call.From, value => interaction.From = value);
        changed |= SetIfDifferent(interaction.To, call.To, value => interaction.To = value);

        if (call.State is CallState.Disconnected or CallState.Failed)
        {
            var endedUtc = _clock.UtcNow;
            var outcome = call.State == CallState.Failed
                ? CallOutcome.Failed
                : CallOutcome.Completed;

            if (interaction.Outcome != outcome)
            {
                interaction.Outcome = outcome;
                changed = true;
            }

            if (interaction.EndedUtc != endedUtc)
            {
                interaction.EndedUtc = endedUtc;
                changed = true;
            }

            var durationSeconds = Math.Max(0, (endedUtc - interaction.StartedUtc).TotalSeconds);

            if (interaction.DurationSeconds != durationSeconds)
            {
                interaction.DurationSeconds = durationSeconds;
                changed = true;
            }
        }

        return changed;
    }

    private static TelephonyCall NormalizeCall(TelephonyInteraction interaction, TelephonyCall call)
    {
        if (string.IsNullOrWhiteSpace(call.CallId))
        {
            call.CallId = interaction.CallId;
        }

        if (string.IsNullOrWhiteSpace(call.ProviderName))
        {
            call.ProviderName = interaction.ProviderName;
        }

        if (string.IsNullOrWhiteSpace(call.From))
        {
            call.From = interaction.From;
        }

        if (string.IsNullOrWhiteSpace(call.To))
        {
            call.To = interaction.To;
        }

        call.Direction = interaction.Direction;
        call.StartedUtc ??= interaction.StartedUtc == default
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(interaction.StartedUtc, DateTimeKind.Utc));

        return call;
    }

    private async Task NotifyUserAsync(string userId, TelephonyCall call)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, userId))
            .CallStateChanged(call);
    }

    private static bool SetIfDifferent(string currentValue, string providerValue, Action<string> setter)
    {
        if (string.IsNullOrWhiteSpace(providerValue) ||
            string.Equals(currentValue, providerValue, StringComparison.Ordinal))
        {
            return false;
        }

        setter(providerValue);

        return true;
    }
}
