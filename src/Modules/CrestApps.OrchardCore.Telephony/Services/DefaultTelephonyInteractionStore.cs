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

    private readonly ISession _session;
    private readonly IStore _store;
    private readonly IProviderIdentityResolver _providerIdentityResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyInteractionStore"/> class.
    /// </summary>
    /// <param name="session">The ambient YesSql session used for reads and creates.</param>
    /// <param name="store">The YesSql store used to open the short isolated sessions that concurrency retries require.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases so a provider and its configuration-backed default variant correlate under a single identity.</param>
    public DefaultTelephonyInteractionStore(
        ISession session,
        IStore store,
        IProviderIdentityResolver providerIdentityResolver)
    {
        _session = session;
        _store = store;
        _providerIdentityResolver = providerIdentityResolver;
    }

    /// <inheritdoc/>
    public Task CreateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        // Canonicalize the provider identity before the interaction is persisted so a call placed through a
        // configuration-backed default provider (for example "Default Asterisk") is stored under the same
        // stable identity ("Asterisk") that the real-time voice event stream projects. Without this, the
        // real-time CallStateChanged projection could never match the interaction and the soft phone would
        // only reflect provider state after a manual refresh.
        interaction.ProviderName = _providerIdentityResolver.Canonicalize(interaction.ProviderName);

        return _session.SaveAsync(interaction, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return _session.SaveAsync(interaction, checkConcurrency: true, cancellationToken: cancellationToken);
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
        for (var attempt = 0; attempt < ConcurrencyRetryLimit; attempt++)
        {
            // A dedicated session keeps the read-decide-write window as short as the database allows and,
            // more importantly, gives the retry a session that has not been canceled by a failed commit.
            await using var session = _store.CreateSession();

            var interaction = await readAsync(session);

            if (interaction is null)
            {
                return null;
            }

            if (!mutate(interaction))
            {
                return interaction;
            }

            try
            {
                await session.SaveAsync(interaction, checkConcurrency: true, cancellationToken: cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // Another writer committed between the read and the save. The mutation was computed from a version
                // that no longer exists, so it must be recomputed against the winner rather than overwriting it.
                continue;
            }

            return interaction;
        }

        throw new InvalidOperationException(
            $"Unable to update the telephony interaction after {ConcurrencyRetryLimit} attempts because concurrent writers kept winning the race.");
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
}
