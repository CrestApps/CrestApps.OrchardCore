using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Dapper;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Measures a dialer profile's recent calls from the durable event log the platform already keeps, for predictive
/// dialing to size its over-dial from.
/// </summary>
/// <remarks>
/// <para>
/// The answered and abandoned counts are taken from <see cref="IDialerAbandonmentStatisticsProvider"/>, the same counts
/// the abandonment cap is enforced on, so pacing steers by exactly the rate it is held to. The calls placed are counted
/// the same way, from the <c>DialerAttemptStarted</c> events filed under the profile.
/// </para>
/// <para>
/// The answer rate and the per-call timings come from the most recent calls (<see
/// cref="ContactCenterPredictiveDialingOptions.MaxTimingSamples"/>), read by <see cref="DialerPacingQueries"/>. A call that
/// is still ringing is left out of the answer rate until it has an outcome.
/// </para>
/// <para>
/// A measurement is reused for <see cref="ContactCenterPredictiveDialingOptions.StatisticsCacheDuration"/> across scopes,
/// and when any part of it cannot be read the whole measurement is <see langword="null"/>: pacing then does not over-dial.
/// </para>
/// </remarks>
public sealed class InteractionEventDialerPacingStatisticsProvider : IDialerPacingStatisticsProvider
{
    private static readonly string _aggregateType = nameof(DialerProfile);

    private readonly ISession _session;
    private readonly IEnumerable<IDialerAbandonmentStatisticsProvider> _abandonmentStatisticsProviders;
    private readonly DialerPacingStatisticsCache _cache;
    private readonly ContactCenterPredictiveDialingOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventDialerPacingStatisticsProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the event log is read through.</param>
    /// <param name="abandonmentStatisticsProviders">The providers of the counts the abandonment cap is enforced on.</param>
    /// <param name="cache">The tenant's cache of recent measurements.</param>
    /// <param name="options">The predictive dialing options.</param>
    /// <param name="clock">The clock the window ends at.</param>
    public InteractionEventDialerPacingStatisticsProvider(
        ISession session,
        IEnumerable<IDialerAbandonmentStatisticsProvider> abandonmentStatisticsProviders,
        DialerPacingStatisticsCache cache,
        IOptions<ContactCenterPredictiveDialingOptions> options,
        IClock clock)
    {
        _session = session;
        _abandonmentStatisticsProviders = abandonmentStatisticsProviders;
        _cache = cache;
        _options = options.Value;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<DialerPacingStatistics> GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dialerProfileId) || window <= TimeSpan.Zero)
        {
            return null;
        }

        var now = _clock.UtcNow;

        if (_cache.TryGet(dialerProfileId, window, now, out var cached))
        {
            return cached;
        }

        var abandonment = await GetAbandonmentStatisticsAsync(dialerProfileId, window, cancellationToken);

        if (abandonment is null)
        {
            return null;
        }

        var fromUtc = now - window;
        var statistics = new DialerPacingStatistics
        {
            FromUtc = fromUtc,
            ToUtc = now,
            Attempts = await CountAttemptsAsync(dialerProfileId, fromUtc, cancellationToken),
            LiveAnswers = abandonment.LiveAnswers,
            AbandonedCalls = abandonment.AbandonedCalls,
        };

        var rows = await ReadCallTimingsAsync(dialerProfileId, fromUtc, cancellationToken);

        ApplySamples(statistics, rows);

        _cache.Set(dialerProfileId, window, statistics, now + _options.StatisticsCacheDuration);

        return statistics;
    }

    /// <summary>
    /// Fills the answer rate and the per-call timings of a measurement from the sampled calls.
    /// </summary>
    /// <param name="statistics">The measurement to fill.</param>
    /// <param name="rows">The sampled calls.</param>
    internal static void ApplySamples(DialerPacingStatistics statistics, IEnumerable<DialerCallTimingRow> rows)
    {
        var ringToAnswer = new List<TimeSpan>();
        var connectLatency = new List<TimeSpan>();
        var talkTime = new List<TimeSpan>();
        var wrapUpTime = new List<TimeSpan>();

        foreach (var row in rows)
        {
            var started = AsUtc(row.StartedUtc);
            var liveAnswered = AsUtc(row.LiveAnsweredUtc);
            var agentJoined = AsUtc(row.AgentJoinedUtc);
            var ended = AsUtc(row.EndedUtc);

            // A call with neither an answer nor an end is still ringing: it has no outcome yet.
            if (liveAnswered is null && ended is null)
            {
                continue;
            }

            statistics.SettledAttempts++;

            if (liveAnswered is not { } answered)
            {
                continue;
            }

            statistics.SettledLiveAnswers++;
            AddIfNotNegative(ringToAnswer, answered - started);

            if (agentJoined is { } joined)
            {
                AddIfNotNegative(connectLatency, joined - answered);

                if (ended is { } callEnded)
                {
                    AddIfNotNegative(talkTime, callEnded - joined);
                }
            }

            if (AsUtc(row.WrapUpStartedUtc) is { } wrapStarted && AsUtc(row.WrapUpCompletedUtc) is { } wrapCompleted)
            {
                AddIfNotNegative(wrapUpTime, wrapCompleted - wrapStarted);
            }
        }

        ringToAnswer.Sort();
        connectLatency.Sort();

        statistics.RingToAnswerSamples = ringToAnswer.Count;
        statistics.MedianRingToAnswer = Percentile(ringToAnswer, 0.5);
        statistics.P75RingToAnswer = Percentile(ringToAnswer, 0.75);
        statistics.ConnectLatencySamples = connectLatency.Count;
        statistics.MedianConnectLatency = Percentile(connectLatency, 0.5);
        statistics.P95ConnectLatency = Percentile(connectLatency, 0.95);
        statistics.TalkTimeSamples = talkTime.Count;
        statistics.AverageTalkTime = Average(talkTime);
        statistics.WrapUpTimeSamples = wrapUpTime.Count;
        statistics.AverageWrapUpTime = Average(wrapUpTime);
    }

    // Nearest-rank: the smallest sample at or above the requested share of the samples, so a percentile is always a time
    // a call actually took.
    private static TimeSpan? Percentile(List<TimeSpan> sorted, double share)
    {
        if (sorted.Count == 0)
        {
            return null;
        }

        var rank = (int)Math.Ceiling(share * sorted.Count);

        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }

    private static TimeSpan? Average(List<TimeSpan> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        return TimeSpan.FromTicks((long)samples.Average(sample => sample.Ticks));
    }

    // Two clocks wrote the moments compared here (the platform's and the provider's events), so a pair can be a few
    // milliseconds out of order. A negative duration is a clock artefact, not a measurement.
    private static void AddIfNotNegative(List<TimeSpan> samples, TimeSpan value)
    {
        if (value >= TimeSpan.Zero)
        {
            samples.Add(value);
        }
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value)
        => value is { } actual ? AsUtc(actual) : null;

    private async Task<DialerAbandonmentStatistics> GetAbandonmentStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken)
    {
        foreach (var provider in _abandonmentStatisticsProviders)
        {
            var statistics = await provider.GetStatisticsAsync(dialerProfileId, window, cancellationToken);

            if (statistics is not null)
            {
                return statistics;
            }
        }

        return null;
    }

    private async Task<long> CountAttemptsAsync(string dialerProfileId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        var aggregateType = _aggregateType;
        var eventType = ContactCenterConstants.Events.DialerAttemptStarted;

        return await _session.QueryIndex<InteractionEventIndex>(
            index => index.AggregateType == aggregateType &&
                index.AggregateId == dialerProfileId &&
                index.OccurredUtc >= fromUtc &&
                index.EventType == eventType,
            collection: ContactCenterStorage.CollectionName)
            .CountAsync(cancellationToken);
    }

    private async Task<IEnumerable<DialerCallTimingRow>> ReadCallTimingsAsync(string dialerProfileId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        var sql = DialerPacingQueries.BuildCallTimingsSql(_session.Store.Configuration, _options.MaxTimingSamples);
        var parameters = new DynamicParameters();
        parameters.Add(DialerPacingQueries.AggregateTypeParameter, _aggregateType);
        parameters.Add(DialerPacingQueries.ProfileIdParameter, dialerProfileId);
        parameters.Add(DialerPacingQueries.FromUtcParameter, fromUtc);
        parameters.Add(DialerPacingQueries.AttemptStartedParameter, ContactCenterConstants.Events.DialerAttemptStarted);
        parameters.Add(DialerPacingQueries.LiveAnsweredParameter, ContactCenterConstants.Events.DialerLiveAnswered);
        parameters.Add(DialerPacingQueries.AgentLegAnsweredParameter, ContactCenterConstants.Events.AgentLegAnswered);

        // The statement runs on the session's own transaction, so it first has to see what this unit of work wrote.
        await _session.FlushAsync(cancellationToken);
        var transaction = await _session.BeginTransactionAsync(cancellationToken);

        var rows = await transaction.Connection.QueryAsync<RawCallTimingRow>(
            new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken));

        return rows.Select(row => new DialerCallTimingRow
        {
            InteractionId = row.InteractionId,
            StartedUtc = ReadUtc(row.StartedUtc) ?? default,
            LiveAnsweredUtc = ReadUtc(row.LiveAnsweredUtc),
            AgentJoinedUtc = ReadUtc(row.AgentJoinedUtc),
            EndedUtc = ReadUtc(row.EndedUtc),
            WrapUpStartedUtc = ReadUtc(row.WrapUpStartedUtc),
            WrapUpCompletedUtc = ReadUtc(row.WrapUpCompletedUtc),
        }).ToList();
    }

    // A value read through a sub-select loses its column type on SQLite, which hands back the stored text instead of a
    // date; the other engines hand back a date. Both are accepted, and both are UTC.
    internal static DateTime? ReadUtc(object value)
    {
        return value switch
        {
            null or DBNull => null,
            DateTime date => AsUtc(date),
            DateTimeOffset offset => offset.UtcDateTime,
            string text when DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed) => DateTime.SpecifyKind(parsed, DateTimeKind.Utc),
            _ => null,
        };
    }

    private sealed class RawCallTimingRow
    {
        public string InteractionId { get; set; }

        public object StartedUtc { get; set; }

        public object LiveAnsweredUtc { get; set; }

        public object AgentJoinedUtc { get; set; }

        public object EndedUtc { get; set; }

        public object WrapUpStartedUtc { get; set; }

        public object WrapUpCompletedUtc { get; set; }
    }
}
