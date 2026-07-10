using CrestApps.OrchardCore.Telephony.Indexes;
using CrestApps.OrchardCore.Telephony.Models;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyInteractionStore"/> backed by YesSql.
/// </summary>
public sealed class DefaultTelephonyInteractionStore : ITelephonyInteractionStore
{
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
