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

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyInteractionStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public DefaultTelephonyInteractionStore(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public Task CreateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return _session.SaveAsync(interaction, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(TelephonyInteraction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return _session.SaveAsync(interaction, cancellationToken: cancellationToken);
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

        return await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x => x.ProviderName == providerName && x.CallId == callId)
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
    public async Task<IReadOnlyList<TelephonyInteraction>> ListActiveByUserAsync(
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
    public async Task<IReadOnlyList<TelephonyInteraction>> ListActiveAsync(int maxCount, CancellationToken cancellationToken = default)
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
    public async Task<IReadOnlyList<TelephonyInteraction>> ListActiveAsync(
        string providerName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var take = maxCount <= 0 ? DefaultReconciliationBatchSize : maxCount;
        var interactions = await _session
            .Query<TelephonyInteraction, TelephonyInteractionIndex>(x =>
                x.ProviderName == providerName &&
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
}
