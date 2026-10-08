using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Counts a dialer profile's rolling abandonment from the durable event log: the <c>DialerLiveAnswered</c> and
/// <c>DialerCallAbandoned</c> facts <see cref="IDialerAbandonmentTracker"/> files under the profile.
/// </summary>
/// <remarks>
/// <para>
/// A live answer is a call a person, not a machine, answered for a Power, Progressive or Predictive profile; calls that
/// rang out, were busy, failed, reached a number not in service or were answered by a machine are not counted at all. An
/// abandoned call is a live answer no agent was connected to within two seconds: the reserved agent never connected,
/// was connected late, or the person hung up while waiting. Each is counted in the window it happened in.
/// </para>
/// <para>
/// Both counts are single seeks on the event index's aggregate key (aggregate type, profile, time), and the result is kept
/// for the rest of the scope, so one dialing cycle that evaluates the policy for every reserved agent counts once.
/// </para>
/// </remarks>
public sealed class InteractionEventDialerAbandonmentStatisticsProvider : IDialerAbandonmentStatisticsProvider
{
    private static readonly string _aggregateType = nameof(DialerProfile);

    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly Dictionary<(string ProfileId, TimeSpan Window), DialerAbandonmentStatistics> _cache = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventDialerAbandonmentStatisticsProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the event log is read through.</param>
    /// <param name="clock">The clock the window ends at.</param>
    public InteractionEventDialerAbandonmentStatisticsProvider(
        ISession session,
        IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialerAbandonmentStatistics> GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dialerProfileId) || window <= TimeSpan.Zero)
        {
            return null;
        }

        if (_cache.TryGetValue((dialerProfileId, window), out var cached))
        {
            return cached;
        }

        var fromUtc = _clock.UtcNow - window;

        var statistics = new DialerAbandonmentStatistics
        {
            LiveAnswers = await CountAsync(dialerProfileId, ContactCenterConstants.Events.DialerLiveAnswered, fromUtc, cancellationToken),
            AbandonedCalls = await CountAsync(dialerProfileId, ContactCenterConstants.Events.DialerCallAbandoned, fromUtc, cancellationToken),
        };

        _cache[(dialerProfileId, window)] = statistics;

        return statistics;
    }

    private async Task<long> CountAsync(string dialerProfileId, string eventType, DateTime fromUtc, CancellationToken cancellationToken)
    {
        var aggregateType = _aggregateType;

        return await _session.QueryIndex<InteractionEventIndex>(
            index => index.AggregateType == aggregateType &&
                index.AggregateId == dialerProfileId &&
                index.OccurredUtc >= fromUtc &&
                index.EventType == eventType,
            collection: ContactCenterStorage.CollectionName)
            .CountAsync(cancellationToken);
    }
}
