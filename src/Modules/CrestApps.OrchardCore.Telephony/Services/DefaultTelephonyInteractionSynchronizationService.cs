using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyInteractionSynchronizationService"/> that reconciles locally persisted
/// telephony interactions against provider-authoritative call state.
/// </summary>
/// <remarks>
/// Reconciliation is provider-agnostic: it resolves the provider for each interaction by its technical name and,
/// only when that provider implements <see cref="ITelephonyCallStateProvider"/>, queries the provider for the
/// current call state. Interactions the provider still reports as active are surfaced, interactions the provider
/// reports as ended are finalized, and interactions the provider no longer knows about are treated as orphans and
/// removed. When a provider cannot be resolved or cannot report call state, the persisted interaction is surfaced
/// unchanged so history and the soft phone view remain available.
/// </remarks>
public sealed class DefaultTelephonyInteractionSynchronizationService : ITelephonyInteractionSynchronizationService
{
    private readonly ITelephonyInteractionStore _store;
    private readonly ITelephonyProviderResolver _resolver;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyInteractionSynchronizationService"/> class.
    /// </summary>
    /// <param name="store">The interaction store used to read, finalize, and remove interactions.</param>
    /// <param name="resolver">The provider resolver used to obtain the provider for each interaction.</param>
    /// <param name="clock">The clock used to stamp finalized interactions.</param>
    /// <param name="logger">The logger.</param>
    public DefaultTelephonyInteractionSynchronizationService(
        ITelephonyInteractionStore store,
        ITelephonyProviderResolver resolver,
        IClock clock,
        ILogger<DefaultTelephonyInteractionSynchronizationService> logger)
    {
        _store = store;
        _resolver = resolver;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallLookupResult> GetActiveCallAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            };
        }

        var interaction = await _store.FindActiveByUserAsync(userId, cancellationToken);

        if (interaction is null)
        {
            return new TelephonyCallLookupResult
            {
                Succeeded = true,
                Found = false,
            };
        }

        var reconciliation = await ReconcileAsync(interaction, cancellationToken);

        return new TelephonyCallLookupResult
        {
            Succeeded = !reconciliation.LookupFailed,
            Found = reconciliation.Call is not null,
            Call = reconciliation.Call,
        };
    }

    /// <inheritdoc/>
    public async Task<TelephonyCallListLookupResult> GetActiveCallsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return new TelephonyCallListLookupResult
            {
                Succeeded = true,
                Calls = [],
            };
        }

        var interactions = await _store.ListActiveByUserAsync(userId, cancellationToken);
        var calls = new List<TelephonyCall>();
        var succeeded = true;

        foreach (var interaction in interactions)
        {
            var reconciliation = await ReconcileAsync(interaction, cancellationToken);

            if (reconciliation.LookupFailed)
            {
                succeeded = false;
            }

            if (reconciliation.Call is not null)
            {
                calls.Add(reconciliation.Call);
            }
        }

        return new TelephonyCallListLookupResult
        {
            Succeeded = succeeded,
            Calls = calls,
        };
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default)
    {
        var interactions = await _store.ListActiveAsync(0, cancellationToken);

        return await ReconcileManyAsync(interactions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileProviderInteractionsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var interactions = await _store.ListActiveAsync(providerName, 0, cancellationToken);

        return await ReconcileManyAsync(interactions, cancellationToken);
    }

    private async Task<int> ReconcileManyAsync(IReadOnlyList<TelephonyInteraction> interactions, CancellationToken cancellationToken)
    {
        var changes = 0;

        foreach (var interaction in interactions)
        {
            var reconciliation = await ReconcileAsync(interaction, cancellationToken);

            if (reconciliation.Changed)
            {
                changes++;
            }
        }

        return changes;
    }

    private async Task<ReconciliationOutcome> ReconcileAsync(TelephonyInteraction interaction, CancellationToken cancellationToken)
    {
        var provider = await _resolver.GetAsync(interaction.ProviderName);

        // The provider is gone or cannot report call state, so the persisted interaction is the best truth
        // available. Surface it unchanged rather than destroying history for a provider that is merely absent.
        if (provider is not ITelephonyCallStateProvider stateProvider)
        {
            return ReconciliationOutcome.Unverified(BuildFallbackCall(interaction));
        }

        TelephonyCallLookupResult lookup;

        try
        {
            lookup = await stateProvider.GetCallStateAsync(interaction.CallId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to query telephony provider {ProviderName} for the state of call {CallId} while reconciling interaction {InteractionId}.",
                interaction.ProviderName,
                interaction.CallId,
                interaction.InteractionId);

            return ReconciliationOutcome.Failed(BuildFallbackCall(interaction));
        }

        if (lookup is null || !lookup.Succeeded)
        {
            return ReconciliationOutcome.Failed(BuildFallbackCall(interaction));
        }

        // The provider no longer recognizes the call. A completed call would have been finalized, so an
        // interaction still marked in progress here is an orphan and is removed.
        if (!lookup.Found)
        {
            await _store.DeleteAsync(interaction, cancellationToken);

            return ReconciliationOutcome.Removed();
        }

        var call = lookup.Call ?? BuildFallbackCall(interaction);

        if (string.IsNullOrEmpty(call.CallId))
        {
            call.CallId = interaction.CallId;
        }

        if (string.IsNullOrEmpty(call.ProviderName))
        {
            call.ProviderName = interaction.ProviderName;
        }

        // The provider reports the call as terminated. Finalize the persisted interaction so history reflects
        // the real outcome instead of leaving it perpetually in progress.
        if (call.State is CallState.Disconnected or CallState.Failed)
        {
            await FinalizeAsync(interaction, call.State, cancellationToken);

            return ReconciliationOutcome.Finalized();
        }

        var changed = await SyncActiveAsync(interaction, call, cancellationToken);

        return ReconciliationOutcome.Active(call, changed);
    }

    private async Task FinalizeAsync(TelephonyInteraction interaction, CallState state, CancellationToken cancellationToken)
    {
        var endedUtc = _clock.UtcNow;
        var outcome = state == CallState.Failed
            ? CallOutcome.Failed
            : CallOutcome.Completed;

        await _store.UpdateByIdAsync(
            interaction.InteractionId,
            persisted =>
            {
                if (persisted.Outcome != CallOutcome.InProgress)
                {
                    return false;
                }

                persisted.Outcome = outcome;
                persisted.EndedUtc = endedUtc;
                persisted.DurationSeconds = Math.Max(0, (endedUtc - persisted.StartedUtc).TotalSeconds);

                return true;
            },
            cancellationToken);
    }

    private async Task<bool> SyncActiveAsync(TelephonyInteraction interaction, TelephonyCall call, CancellationToken cancellationToken)
    {
        var from = call.From;
        var to = call.To;

        if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to))
        {
            return false;
        }

        var changed = false;

        await _store.UpdateByIdAsync(
            interaction.InteractionId,
            persisted =>
            {
                var mutated = false;

                if (!string.IsNullOrEmpty(from) && !string.Equals(persisted.From, from, StringComparison.Ordinal))
                {
                    persisted.From = from;
                    mutated = true;
                }

                if (!string.IsNullOrEmpty(to) && !string.Equals(persisted.To, to, StringComparison.Ordinal))
                {
                    persisted.To = to;
                    mutated = true;
                }

                changed = mutated;

                return mutated;
            },
            cancellationToken);

        return changed;
    }

    private static TelephonyCall BuildFallbackCall(TelephonyInteraction interaction)
    {
        return new TelephonyCall
        {
            CallId = interaction.CallId,
            From = interaction.From,
            To = interaction.To,
            Direction = interaction.Direction,
            ProviderName = interaction.ProviderName,
            State = CallState.Connected,
            StartedUtc = new DateTimeOffset(DateTime.SpecifyKind(interaction.StartedUtc, DateTimeKind.Utc)),
        };
    }

    private readonly struct ReconciliationOutcome
    {
        private ReconciliationOutcome(TelephonyCall call, bool lookupFailed, bool changed)
        {
            Call = call;
            LookupFailed = lookupFailed;
            Changed = changed;
        }

        public TelephonyCall Call { get; }

        public bool LookupFailed { get; }

        public bool Changed { get; }

        public static ReconciliationOutcome Active(TelephonyCall call, bool changed)
            => new(call, lookupFailed: false, changed);

        public static ReconciliationOutcome Unverified(TelephonyCall call)
            => new(call, lookupFailed: false, changed: false);

        public static ReconciliationOutcome Failed(TelephonyCall call)
            => new(call, lookupFailed: true, changed: false);

        public static ReconciliationOutcome Finalized()
            => new(call: null, lookupFailed: false, changed: true);

        public static ReconciliationOutcome Removed()
            => new(call: null, lookupFailed: false, changed: true);
    }
}
