using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Reconciles locally persisted telephony interactions with provider-authoritative call state.
/// </summary>
public sealed class TelephonyInteractionSynchronizationService : ITelephonyInteractionSynchronizationService
{
    private const string ReconciliationLockKey = "TelephonyInteractionStateReconciliation";
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan _newInteractionGracePeriod = TimeSpan.FromSeconds(15);

    private readonly ITelephonyInteractionStore _interactionStore;
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyInteractionSynchronizationService"/> class.
    /// </summary>
    /// <param name="interactionStore">The telephony interaction store.</param>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="hubContext">The soft-phone hub context.</param>
    /// <param name="distributedLock">The distributed lock used to prevent overlapping reconciliation sweeps.</param>
    /// <param name="clock">The clock used to stamp terminal interactions.</param>
    /// <param name="logger">The logger.</param>
    public TelephonyInteractionSynchronizationService(
        ITelephonyInteractionStore interactionStore,
        ITelephonyProviderResolver providerResolver,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<TelephonyInteractionSynchronizationService> logger)
    {
        _interactionStore = interactionStore;
        _providerResolver = providerResolver;
        _hubContext = hubContext;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
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

        var (lookup, _) = await RefreshInteractionAsync(interaction, notifyUser: true, cancellationToken);

        return lookup;
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
            ? await _interactionStore.ListActiveAsync(cancellationToken)
            : await _interactionStore.ListActiveAsync(providerName, cancellationToken);
        var changed = 0;

        foreach (var interaction in interactions)
        {
            var (_, interactionChanged) = await RefreshInteractionAsync(
                interaction,
                notifyUser: true,
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
        bool notifyUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(interaction.ProviderName) ||
            string.IsNullOrWhiteSpace(interaction.CallId))
        {
            await RemoveOrphanAsync(
                interaction,
                notifyUser,
                "the interaction does not contain a complete provider identity",
                cancellationToken);

            return (new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            }, true);
        }

        var provider = await _providerResolver.GetAsync(interaction.ProviderName);

        if (provider is not ITelephonyCallStateProvider stateProvider)
        {
            var error = $"Provider '{interaction.ProviderName}' does not support authoritative call-state lookup.";

            _logger.LogWarning(
                "Unable to reconcile telephony interaction {InteractionId}: {ErrorMessage}",
                interaction.InteractionId,
                error);

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
                interaction.InteractionId,
                interaction.ProviderName,
                interaction.CallId,
                lookup.Error);

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
                        interaction.InteractionId,
                        interaction.ProviderName,
                        interaction.CallId);
                }

                return (lookup, false);
            }

            await RemoveOrphanAsync(
                interaction,
                notifyUser,
                "the provider no longer reports the call",
                cancellationToken);

            return (lookup, true);
        }

        if (lookup.Call is null)
        {
            var error = $"Provider '{interaction.ProviderName}' returned an empty call state for '{interaction.CallId}'.";

            _logger.LogWarning(
                "Unable to reconcile telephony interaction {InteractionId}: {ErrorMessage}",
                interaction.InteractionId,
                error);

            return (new TelephonyCallLookupResult
            {
                Succeeded = false,
                Found = true,
                Error = error,
            }, false);
        }

        var call = NormalizeCall(interaction, lookup.Call);
        var changed = ApplyProviderState(interaction, call);

        if (changed)
        {
            await _interactionStore.UpdateAsync(interaction, cancellationToken);
        }

        if (notifyUser)
        {
            await NotifyUserAsync(interaction.UserId, call);
        }

        return (lookup, changed);
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
            interaction.InteractionId,
            interaction.ProviderName,
            interaction.CallId,
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
        call.CallId ??= interaction.CallId;
        call.ProviderName ??= interaction.ProviderName;
        call.From ??= interaction.From;
        call.To ??= interaction.To;
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

        await _hubContext.Clients.User(userId).CallStateChanged(call);
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
