namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A timer source driven by the harness's <see cref="TestClock"/>: nothing fires until the test advances time, and
/// advancing fires every timer that falls due along the way, at its own instant, in order. Lets a test show a
/// deadline firing when it is due without waiting for it in real time.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly TestClock _clock;
    private readonly List<ManualTimer> _timers = [];
    private readonly Lock _gate = new();

    public ManualTimeProvider(TestClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Gets how many timers are waiting to fire.
    /// </summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.DueUtc.HasValue);
            }
        }
    }

    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc));

    public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);

        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>
    /// Moves time forward by <paramref name="by"/>, firing each timer that falls due on the way at its due instant and
    /// letting what it started finish (<paramref name="settle"/>) before time moves on, as it would in real time.
    /// </summary>
    public async Task AdvanceAsync(TimeSpan by, Func<Task> settle)
    {
        var target = _clock.UtcNow + by;

        while (true)
        {
            ManualTimer next;

            lock (_gate)
            {
                next = _timers
                    .Where(timer => timer.DueUtc.HasValue && timer.DueUtc.Value <= target)
                    .OrderBy(timer => timer.DueUtc.Value)
                    .FirstOrDefault();
            }

            if (next is null)
            {
                break;
            }

            if (next.DueUtc.Value > _clock.UtcNow)
            {
                _clock.Advance(next.DueUtc.Value - _clock.UtcNow);
            }

            next.Fire();
            await settle();
        }

        if (target > _clock.UtcNow)
        {
            _clock.Advance(target - _clock.UtcNow);
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object _state;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public DateTime? DueUtc { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan)
            {
                throw new NotSupportedException("Only one-shot timers are modelled.");
            }

            DueUtc = dueTime == Timeout.InfiniteTimeSpan
                ? null
                : _owner._clock.UtcNow + dueTime;

            return true;
        }

        public void Fire()
        {
            DueUtc = null;
            _callback(_state);
        }

        public void Dispose()
        {
            DueUtc = null;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
