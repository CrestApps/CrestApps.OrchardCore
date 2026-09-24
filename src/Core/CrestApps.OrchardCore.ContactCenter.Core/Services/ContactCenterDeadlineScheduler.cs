using System.Collections.Concurrent;
using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds routing deadlines in process and runs each one on a fresh shell scope the moment it falls due.
/// </summary>
/// <remarks>
/// A tenant singleton. The work runs on a scope taken from the shell host, never on the scope that scheduled it:
/// that scope is usually a webhook or a request that has long been answered by the time the deadline arrives, and
/// anything attached to it is gone. The shell scope commits the session when the work returns and then runs the
/// work's after-commit tasks, which is what sends the realtime revocation to the agent's screens and dispatches any
/// provider command the work registered.
/// </remarks>
public sealed class ContactCenterDeadlineScheduler : IContactCenterDeadlineScheduler, IDisposable
{
    /// <summary>
    /// How long after its deadline a piece of work runs. The work re-reads the deadline against the tenant clock and
    /// does nothing while it has not passed, so running a hair late is what keeps a timer that fires a little early
    /// from having to be run twice.
    /// </summary>
    internal static readonly TimeSpan FireGrace = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The shortest wait before work that asked to run again is run again, so work that keeps answering "now" cannot
    /// spin.
    /// </summary>
    internal static readonly TimeSpan MinimumRescheduleDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How far ahead a deadline is held. Anything further out is left to the sweeps, so a long wait does not pin a
    /// timer for its whole length.
    /// </summary>
    internal static readonly TimeSpan MaximumLeadTime = TimeSpan.FromHours(1);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Task, byte> _running = new();
    private readonly CancellationTokenSource _disposed = new();
    private readonly IClock _clock;
    private readonly TimeProvider _timeProvider;
    private readonly Func<Func<IServiceProvider, Task>, Task> _runInScope;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDeadlineScheduler"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host, used to open a scope that outlives whatever scheduled the work.</param>
    /// <param name="shellSettings">The tenant the deadlines belong to.</param>
    /// <param name="clock">The tenant clock deadlines are measured against.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterDeadlineScheduler(
        IShellHost shellHost,
        ShellSettings shellSettings,
        IClock clock,
        ILogger<ContactCenterDeadlineScheduler> logger)
        : this(clock, TimeProvider.System, work => RunInShellScopeAsync(shellHost, shellSettings, work), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDeadlineScheduler"/> class over an explicit timer
    /// source and scope, so a test can drive time and run the work against its own services.
    /// </summary>
    internal ContactCenterDeadlineScheduler(
        IClock clock,
        TimeProvider timeProvider,
        Func<Func<IServiceProvider, Task>, Task> runInScope,
        ILogger logger)
    {
        _clock = clock;
        _timeProvider = timeProvider;
        _runInScope = runInScope;
        _logger = logger;
    }

    /// <summary>
    /// Gets the number of deadlines currently held.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <inheritdoc/>
    public void Schedule(string key, DateTime dueUtc, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(work);

        TrySchedule(key, dueUtc, work, replaceExisting: true, minimumDelay: TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public void Cancel(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        Entry removed;

        lock (_gate)
        {
            if (!_entries.Remove(key, out removed))
            {
                return;
            }
        }

        removed.Timer.Dispose();
    }

    /// <summary>
    /// Waits for every piece of work that has started to finish, so a test can observe its outcome.
    /// </summary>
    internal async Task WhenIdleAsync()
    {
        while (!_running.IsEmpty)
        {
            await Task.WhenAll(_running.Keys);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed.IsCancellationRequested)
        {
            return;
        }

        _disposed.Cancel();

        Entry[] entries;

        lock (_gate)
        {
            entries = [.. _entries.Values];
            _entries.Clear();
        }

        foreach (var entry in entries)
        {
            entry.Timer.Dispose();
        }

        _disposed.Dispose();
    }

    private void TrySchedule(
        string key,
        DateTime dueUtc,
        Func<IServiceProvider, CancellationToken, Task<DateTime?>> work,
        bool replaceExisting,
        TimeSpan minimumDelay)
    {
        if (_disposed.IsCancellationRequested)
        {
            return;
        }

        var delay = dueUtc - _clock.UtcNow;

        if (delay > MaximumLeadTime)
        {
            // Too far out to be worth a timer: the sweep will reach it long before it matters.
            return;
        }

        delay += FireGrace;

        if (delay < minimumDelay)
        {
            delay = minimumDelay;
        }

        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var entry = new Entry(work);
        Entry replaced = null;

        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (!replaceExisting)
                {
                    // Something scheduled this key afresh while the work ran; that deadline is the newer one.
                    return;
                }

                replaced = existing;
            }

            _entries[key] = entry;

            // Created under the gate so a timer that fires at once still finds its own entry.
            entry.Timer = _timeProvider.CreateTimer(
                static state => ((DeadlineState)state).Owner.OnDue((DeadlineState)state),
                new DeadlineState(this, key, entry),
                delay,
                Timeout.InfiniteTimeSpan);
        }

        replaced?.Timer.Dispose();
    }

    private void OnDue(DeadlineState state)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(state.Key, out var current) || !ReferenceEquals(current, state.Entry))
            {
                // Cancelled or replaced after the timer had already started to fire.
                return;
            }

            _entries.Remove(state.Key);
        }

        state.Entry.Timer.Dispose();

        if (_disposed.IsCancellationRequested)
        {
            return;
        }

        var run = RunAsync(state.Key, state.Entry.Work);
        _running.TryAdd(run, 0);
        _ = run.ContinueWith(
            static (completed, running) => ((ConcurrentDictionary<Task, byte>)running).TryRemove(completed, out _),
            _running,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunAsync(string key, Func<IServiceProvider, CancellationToken, Task<DateTime?>> work)
    {
        // Off the timer thread before anything else, so the timer queue is never held by a database round trip.
        await Task.Yield();

        DateTime? next = null;

        try
        {
            var token = _disposed.Token;

            await _runInScope(async services => next = await work(services, token));
        }
        catch (OperationCanceledException) when (_disposed.IsCancellationRequested)
        {
            return;
        }
        catch (ObjectDisposedException) when (_disposed.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            // The sweep is still the backstop for this deadline, so a failure here costs precision, not the work.
            _logger.LogError(ex, "The Contact Center deadline '{DeadlineKey}' could not be processed; the background sweep will pick it up.", key.SanitizeLogValue());

            return;
        }

        if (next is DateTime nextDueUtc)
        {
            TrySchedule(key, nextDueUtc, work, replaceExisting: false, minimumDelay: MinimumRescheduleDelay);
        }
    }

    private static async Task RunInShellScopeAsync(IShellHost shellHost, ShellSettings shellSettings, Func<IServiceProvider, Task> work)
    {
        var scope = await shellHost.GetScopeAsync(shellSettings);

        await scope.UsingAsync(shellScope => work(shellScope.ServiceProvider));
    }

    private sealed class Entry
    {
        public Entry(Func<IServiceProvider, CancellationToken, Task<DateTime?>> work)
        {
            Work = work;
        }

        public Func<IServiceProvider, CancellationToken, Task<DateTime?>> Work { get; }

        public ITimer Timer { get; set; }
    }

    private sealed record DeadlineState(ContactCenterDeadlineScheduler Owner, string Key, Entry Entry);
}
