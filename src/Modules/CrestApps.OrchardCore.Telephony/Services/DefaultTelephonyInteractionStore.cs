using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyInteractionStore"/> backed by YesSql.
/// </summary>
public sealed class DefaultTelephonyInteractionStore : ITelephonyInteractionStore
{
    private const int DefaultReconciliationBatchSize = 200;
    private const int ConcurrencyRetryLimit = 5;
    private const int DatabaseBusyRetryLimit = 3;
    private const int DatabaseBusyBaseDelayMilliseconds = 200;

    private readonly ISession _session;
    private readonly IStore _store;
    private readonly IProviderIdentityResolver _providerIdentityResolver;
    private readonly IEnumerable<ITelephonyCallObserver> _callObservers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyInteractionStore"/> class.
    /// </summary>
    /// <param name="session">The ambient YesSql session used for reads and creates.</param>
    /// <param name="store">The YesSql store used to open the short isolated sessions that concurrency retries require.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases so a provider and its configuration-backed default variant correlate under a single identity.</param>
    /// <param name="callObservers">The observers told when a saved call has started or ended.</param>
    public DefaultTelephonyInteractionStore(
        ISession session,
        IStore store,
        IProviderIdentityResolver providerIdentityResolver,
        IEnumerable<ITelephonyCallObserver> callObservers)
    {
        _session = session;
        _store = store;
        _providerIdentityResolver = providerIdentityResolver;
        _callObservers = callObservers;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // Canonicalize the provider identity before the interaction is persisted so a call placed through a
        // configuration-backed default provider (for example "Default Asterisk") is stored under the same
        // stable identity ("Asterisk") that the real-time voice event stream projects. Without this, the
        // real-time CallStateChanged projection could never match the interaction and the soft phone would
        // only reflect provider state after a manual refresh.
        interaction.ProviderName = _providerIdentityResolver.Canonicalize(interaction.ProviderName);

        await _session.SaveAsync(interaction, cancellationToken: cancellationToken);

        foreach (var observer in _callObservers)
        {
            await observer.CallStartedAsync(interaction, cancellationToken);
        }

        await NotifyIfEndedAsync(interaction, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        await _session.SaveAsync(interaction, checkConcurrency: true, cancellationToken: cancellationToken);
        await NotifyIfEndedAsync(interaction, cancellationToken);
    }

    // Every writer that settles a call saves it through this store, so this is the one place its end is seen,
    // whichever path -- the soft phone, the provider's events or reconciliation -- settled it.
    private async Task NotifyIfEndedAsync(TelephonyInteraction interaction, CancellationToken cancellationToken)
    {
        if (interaction.Outcome == CallOutcome.InProgress || !interaction.EndedUtc.HasValue)
        {
            return;
        }

        foreach (var observer in _callObservers)
        {
            await observer.CallEndedAsync(interaction, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task<TelephonyInteraction> UpdateByIdAsync(
        string interactionId,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(interactionId);
        ArgumentNullException.ThrowIfNull(mutate);

        return MutateWithRetryAsync(
            session => session
                .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.InteractionId == interactionId)
                .FirstOrDefaultAsync(cancellationToken),
            mutate,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyInteraction> UpdateByProviderCallIdAsync(
        string providerName,
        string callId,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentNullException.ThrowIfNull(mutate);

        var canonicalProviderName = _providerIdentityResolver.Canonicalize(providerName);

        return MutateWithRetryAsync(
            session => session
                .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.ProviderName == canonicalProviderName && x.CallId == callId)
                .FirstOrDefaultAsync(cancellationToken),
            mutate,
            cancellationToken);
    }

    private async Task<TelephonyInteraction> MutateWithRetryAsync(
        Func<ISession, Task<TelephonyInteraction>> readAsync,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken)
    {
        // The isolated session below is a second writer. When the caller's own session has already written (a
        // provider webhook records the hangup's call quality before this projection runs), that open transaction
        // holds the database's only write lock on SQLite, and the isolated write would wait on its own caller until
        // the busy timeout ran out. Committing what the caller has done first leaves nothing of its own to wait on.
        if (_session.CurrentTransaction is not null)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        var concurrencyAttempts = 0;
        var busyAttempts = 0;

        while (true)
        {
            try
            {
                var (completed, interaction) = await TryMutateAsync(readAsync, mutate, cancellationToken);

                if (completed)
                {
                    return interaction;
                }
            }
            catch (Exception exception) when (TransientDatabaseErrors.IsTransient(exception) && busyAttempts < DatabaseBusyRetryLimit)
            {
                // Another writer held the database for longer than one attempt's busy timeout. The attempt's session
                // is discarded with its failed commit, so the next attempt reads and decides afresh. A database that
                // stays busy past the last retry is left to fail, so the caller (the durable webhook inbox) schedules
                // the delivery again instead of the error being swallowed here.
                busyAttempts++;
                await Task.Delay(TimeSpan.FromMilliseconds(DatabaseBusyBaseDelayMilliseconds << (busyAttempts - 1)), cancellationToken);

                continue;
            }

            // Another writer committed between the read and the save. The mutation was computed from a version that
            // no longer exists, so it must be recomputed against the winner rather than overwriting it.
            if (++concurrencyAttempts >= ConcurrencyRetryLimit)
            {
                throw new InvalidOperationException(
                    $"Unable to update the telephony interaction after {ConcurrencyRetryLimit} attempts because concurrent writers kept winning the race.");
            }
        }
    }

    private async Task<(bool Completed, TelephonyInteraction Interaction)> TryMutateAsync(
        Func<ISession, Task<TelephonyInteraction>> readAsync,
        Func<TelephonyInteraction, bool> mutate,
        CancellationToken cancellationToken)
    {
        // A dedicated session keeps the read-decide-write window as short as the database allows and, more
        // importantly, gives the retry a session that has not been canceled by a failed commit.
        await using var session = _store.CreateSession();

        var interaction = await readAsync(session);

        if (interaction is null)
        {
            return (true, null);
        }

        var wasInProgress = interaction.Outcome == CallOutcome.InProgress || !interaction.EndedUtc.HasValue;

        if (!mutate(interaction))
        {
            return (true, interaction);
        }

        try
        {
            await session.SaveAsync(interaction, checkConcurrency: true, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return (false, null);
        }

        if (wasInProgress)
        {
            await NotifyIfEndedAsync(interaction, cancellationToken);
        }

        return (true, interaction);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        _session.Delete(interaction);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<TelephonyInteraction> FindByCallIdAsync(string userId, string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(callId))
        {
            return null;
        }

        return await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.UserId == userId && x.CallId == callId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyInteraction> FindByInteractionIdAsync(string userId, string interactionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(interactionId))
        {
            return null;
        }

        return await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.UserId == userId && x.InteractionId == interactionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyInteraction> FindByProviderCallIdAsync(string providerName, string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(callId))
        {
            return null;
        }

        var canonicalProviderName = _providerIdentityResolver.Canonicalize(providerName);

        return await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.ProviderName == canonicalProviderName && x.CallId == callId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TelephonyInteraction> FindActiveByUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        return await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x =>
                x.UserId == userId &&
                x.Outcome == CallOutcome.InProgress)
            .OrderByDescending(x => x.StartedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelephonyInteraction>> GetActiveByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x =>
                x.UserId == userId &&
                x.Outcome == CallOutcome.InProgress)
            .OrderByDescending(x => x.StartedUtc)
            .ListAsync(cancellationToken);

        return interactions.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var take = maxCount <= 0 ? DefaultReconciliationBatchSize : maxCount;
        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.Outcome == CallOutcome.InProgress)
            .OrderBy(x => x.StartedUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return interactions.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelephonyInteraction>> GetActiveAsync(
        string providerName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var canonicalProviderName = _providerIdentityResolver.Canonicalize(providerName);
        var take = maxCount <= 0 ? DefaultReconciliationBatchSize : maxCount;
        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x =>
                x.ProviderName == canonicalProviderName &&
                x.Outcome == CallOutcome.InProgress)
            .OrderBy(x => x.StartedUtc)
            .Take(take)
            .ListAsync(cancellationToken);

        return interactions.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelephonyInteraction>> GetRecentAsync(string userId, int count, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.UserId == userId)
            .OrderByDescending(x => x.StartedUtc)
            .Take(count)
            .ListAsync(cancellationToken);

        return interactions.ToList();
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadVoicemailCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        return await _session
            .QueryIndex<TelephonyInteractionIndex>(x =>
                x.UserId == userId &&
                x.IsVoicemail &&
                x.VoicemailReadUtc == null)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<TelephonyInteraction> MarkVoicemailReadAsync(
        string userId,
        string callId,
        DateTime readUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(callId);

        return MutateWithRetryAsync(
            session => session
                .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.UserId == userId && x.CallId == callId)
                .FirstOrDefaultAsync(cancellationToken),
            interaction =>
            {
                // Only a voicemail that has not already been marked read needs a write; anything else is a no-op so
                // repeated marks (for example re-opening the history panel) do not churn the store or move the time.
                if (!interaction.IsVoicemail || interaction.VoicemailReadUtc is not null)
                {
                    return false;
                }

                interaction.VoicemailReadUtc = readUtc;

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> MarkAllVoicemailsReadAsync(string userId, DateTime readUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return 0;
        }

        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x =>
                x.UserId == userId &&
                x.IsVoicemail &&
                x.VoicemailReadUtc == null)
            .ListAsync(cancellationToken);

        var count = 0;

        foreach (var interaction in interactions)
        {
            interaction.VoicemailReadUtc = readUtc;
            await _session.SaveAsync(interaction, cancellationToken: cancellationToken);
            count++;
        }

        return count;
    }
}
