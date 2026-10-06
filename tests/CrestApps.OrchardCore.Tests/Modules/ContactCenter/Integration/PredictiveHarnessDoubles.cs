using System.Collections.Concurrent;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// The doubles every unit of work over one harness database shares, as a tenant's singletons are shared by its scopes.
/// </summary>
internal sealed class HarnessShared
{
    public HarnessShared(bool predictive)
    {
        Predictive = predictive;
        Router.ReportsAnswerDetection = predictive;
    }

    public bool Predictive { get; }

    public InMemoryOmnichannelActivities Activities { get; } = new();

    public FakeVoiceContactCenterCallRouter Router { get; } = new();

    public InMemoryProviderCommandStateService Commands { get; } = new();

    public HarnessDialerProfileReader Profiles { get; } = new();

    public HarnessPredictiveStatistics Statistics { get; } = new();

    public RecordingAbandonedCallMessages Treatment { get; } = new();

    public RecordingDeadlineScheduler Deadlines { get; } = new();

    public RecordingPacingScheduler Pacing { get; } = new();
}

/// <summary>
/// A dialer profile catalog holding the profiles a test registers.
/// </summary>
internal sealed class HarnessDialerProfileReader : IDialerProfileReader
{
    private readonly ConcurrentDictionary<string, DialerProfile> _profiles = new(StringComparer.Ordinal);

    public void Add(DialerProfile profile) => _profiles[profile.ItemId] = profile;

    public ValueTask<DialerProfile> FindByIdAsync(string itemId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(itemId is not null && _profiles.TryGetValue(itemId, out var profile) ? profile : null);

    public Task<IReadOnlyCollection<DialerProfile>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DialerProfile>>(_profiles.Values.Where(profile => profile.Enabled).ToArray());
}

/// <summary>
/// Pacing and abandonment statistics a test sets: the answer rate, the rolling abandonment counts, and the long-run
/// counts (any window of a day or more). With nothing seeded, or after <see cref="MakeUnavailable"/>, every read returns
/// <see langword="null"/>, as a provider that cannot read the store does.
/// </summary>
internal sealed class HarnessPredictiveStatistics : IDialerPacingStatisticsProvider, IDialerAbandonmentStatisticsProvider
{
    private DialerPacingStatistics _pacing;
    private DialerAbandonmentStatistics _rolling;
    private DialerAbandonmentStatistics _compliance;

    public void Seed(double answerRate, long settledAttempts, long liveAnswers, long abandonedCalls, long complianceAbandonedCalls)
    {
        _pacing = new DialerPacingStatistics
        {
            Attempts = settledAttempts,
            SettledAttempts = settledAttempts,
            SettledLiveAnswers = (long)Math.Round(settledAttempts * answerRate),
            LiveAnswers = liveAnswers,
            AbandonedCalls = abandonedCalls,
        };
        _rolling = new DialerAbandonmentStatistics { LiveAnswers = liveAnswers, AbandonedCalls = abandonedCalls };
        _compliance = new DialerAbandonmentStatistics { LiveAnswers = liveAnswers, AbandonedCalls = complianceAbandonedCalls };
    }

    public void MakePacingUnavailable() => _pacing = null;

    public void MakeUnavailable()
    {
        _pacing = null;
        _rolling = null;
        _compliance = null;
    }

    Task<DialerPacingStatistics> IDialerPacingStatisticsProvider.GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken)
        => Task.FromResult(_pacing);

    Task<DialerAbandonmentStatistics> IDialerAbandonmentStatisticsProvider.GetStatisticsAsync(string dialerProfileId, TimeSpan window, CancellationToken cancellationToken)
        => Task.FromResult(window >= TimeSpan.FromDays(1) ? _compliance : _rolling);
}

/// <summary>
/// Records the abandoned-call messages started on calls, standing in for the provider speaking them.
/// </summary>
internal sealed class RecordingAbandonedCallMessages : IQueueTreatmentProvider
{
    public ConcurrentQueue<(string CallId, string Text)> EndedWithMessage { get; } = new();

    public Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopHoldMusicAsync(string providerCallId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartRingbackAsync(string providerCallId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task EndWithMessageAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
    {
        EndedWithMessage.Enqueue((providerCallId, text));

        return Task.CompletedTask;
    }

    public Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Holds in-process deadlines without timers, so a test sees what was scheduled and runs it when it chooses.
/// </summary>
internal sealed class RecordingDeadlineScheduler : IContactCenterDeadlineScheduler
{
    private readonly ConcurrentDictionary<string, (DateTime DueUtc, Func<IServiceProvider, CancellationToken, Task<DateTime?>> Work)> _entries = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> Keys => _entries.Keys.ToArray();

    public void Schedule(string key, DateTime dueUtc, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work)
        => _entries[key] = (dueUtc, work);

    public void Cancel(string key) => _entries.TryRemove(key, out _);

    public async Task RunAsync(string key, IServiceProvider services)
    {
        if (_entries.TryRemove(key, out var entry))
        {
            await entry.Work(services, CancellationToken.None);
        }
    }
}

/// <summary>
/// Records the campaign queues pacing was requested for.
/// </summary>
internal sealed class RecordingPacingScheduler : IPredictivePacingScheduler
{
    public ConcurrentQueue<string> Requests { get; } = new();

    public void Request(string queueId) => Requests.Enqueue(queueId);
}

/// <summary>
/// Stands in for the routing policy: every candidate is eligible, and the agent idle longest is picked.
/// </summary>
internal sealed class HarnessRoutingService : IActivityRoutingService
{
    public Task<ActivityRoutingDecision> SelectAgentAsync(ActivityQueue queue, QueueItem queueItem, IEnumerable<AgentAvailability> availability, CancellationToken cancellationToken = default)
    {
        var candidates = availability
            .OrderBy(candidate => candidate.Agent.LastAssignedUtc ?? DateTime.MinValue)
            .Select(candidate => new ActivityRoutingCandidate(candidate) { IsEligible = true })
            .ToList();

        return Task.FromResult(new ActivityRoutingDecision
        {
            Queue = queue,
            QueueItem = queueItem,
            Agent = candidates.FirstOrDefault()?.Agent,
            Succeeded = candidates.Count > 0,
            Candidates = candidates,
        });
    }
}

/// <summary>
/// A lock that grants every key at once -- as process-local locks on two nodes do -- except that the first
/// <c>parties</c> acquisitions of keys starting with <c>prefix</c> wait for each other first, so the flows taking them
/// set off together. What keeps them apart then is the data, not the lock.
/// </summary>
internal sealed class BarrierDistributedLock : IDistributedLock
{
    private readonly string _prefix;
    private readonly Barrier _barrier;
    private int _arrivals;

    public BarrierDistributedLock(string prefix, int parties)
    {
        _prefix = prefix;
        _barrier = new Barrier(parties);
    }

    public ConcurrentQueue<string> AcquiredKeys { get; } = new();

    public Task<ILocker> AcquireLockAsync(string key, TimeSpan? expiration = null)
        => Task.FromResult<ILocker>(Arrive(key));

    public Task<(ILocker locker, bool locked)> TryAcquireLockAsync(string key, TimeSpan timeout, TimeSpan? expiration = null)
        => Task.FromResult<(ILocker, bool)>((Arrive(key), true));

    public Task<bool> IsLockAcquiredAsync(string key) => Task.FromResult(false);

    private NoLocker Arrive(string key)
    {
        AcquiredKeys.Enqueue(key);

        if (key.StartsWith(_prefix, StringComparison.Ordinal) && Interlocked.Increment(ref _arrivals) <= _barrier.ParticipantCount)
        {
            _barrier.SignalAndWait(TimeSpan.FromSeconds(10));
        }

        return new NoLocker();
    }

    private sealed class NoLocker : ILocker
    {
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
