using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <inheritdoc cref="IAIVoiceCallShutdown"/>
/// <remarks>
/// A tenant singleton, so the registration on the host's stopping token goes with the shell when the tenant is
/// reloaded. Activation makes the registration.
/// </remarks>
public sealed class AIVoiceCallShutdown : ModularTenantEvents, IAIVoiceCallShutdown, IDisposable
{
    /// <summary>
    /// How long the host is held up while the live calls are handed on.
    /// </summary>
    /// <remarks>
    /// Handing a call on takes a second or two: the session closes, the caller is told what is happening, and they
    /// are seated in a queue. The wait holds up the host's own shutdown, which by default allows thirty seconds, so
    /// it stops well short of that whether or not every call made it.
    /// </remarks>
    internal static readonly TimeSpan HandOnTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(50);

    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<Task, byte> _handOns = new();
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    private CancellationTokenRegistration _registration;
    private int _liveCalls;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallShutdown"/> class.
    /// </summary>
    /// <param name="lifetime">The host lifetime whose stopping signal hands the calls on.</param>
    /// <param name="logger">The logger.</param>
    public AIVoiceCallShutdown(IHostApplicationLifetime lifetime, ILogger<AIVoiceCallShutdown> logger)
        : this(lifetime, TimeProvider.System, logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceCallShutdown"/> class over an explicit clock, so a test
    /// can measure the wait.
    /// </summary>
    internal AIVoiceCallShutdown(IHostApplicationLifetime lifetime, TimeProvider timeProvider, ILogger logger)
    {
        _lifetime = lifetime;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public CancellationToken Stopping => _stopping.Token;

    /// <summary>
    /// Gets the number of live calls this node is holding.
    /// </summary>
    internal int LiveCalls => Volatile.Read(ref _liveCalls);

    /// <inheritdoc/>
    public override Task ActivatedAsync()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return Task.CompletedTask;
            }

            _started = true;

            // A host already stopping runs the callback here and now, which is what it should do.
            _registration = _lifetime.ApplicationStopping.Register(static state => ((AIVoiceCallShutdown)state).HandOnLiveCalls(), this);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IDisposable HoldCall()
    {
        Interlocked.Increment(ref _liveCalls);

        return new HeldCall(this);
    }

    /// <inheritdoc/>
    public void TrackHandOn(Task work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (work.IsCompleted)
        {
            return;
        }

        _handOns.TryAdd(work, 0);
        _ = work.ContinueWith(
            static (completed, handOns) => ((ConcurrentDictionary<Task, byte>)handOns).TryRemove(completed, out _),
            _handOns,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Ends every live conversation on this node and waits, briefly, for their callers to be handed on.
    /// </summary>
    /// <remarks>
    /// Blocking is deliberate: the host runs its stopping callbacks before it stops the server, and the server
    /// stopping is what ends the requests these conversations run in. The wait returns at once when no call is
    /// live, which is the usual case.
    /// </remarks>
    internal void HandOnLiveCalls()
    {
        // A call whose session has already ended can still be in the middle of being handed on, so the work in
        // flight counts as much as the calls still live.
        if (IsSettled())
        {
            // Still fired, so a call answered from here on is handed on the moment its conversation starts.
            _stopping.Cancel();

            return;
        }

        _logger.LogWarning(
            "The host is shutting down with {LiveCalls} live AI voice call(s) and {HandOns} being handed on; handing each caller to a person, or ending the call politely where there is nobody to hand to.",
            LiveCalls,
            _handOns.Count);

        // Off this thread: cancelling runs each conversation's teardown, and some of it would otherwise run here,
        // on the thread the host is stopping on.
        _ = Task.Run(() =>
        {
            try
            {
                _stopping.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The tenant was released while the host was stopping; there is nothing left to hand on.
            }
        });

        var deadline = _timeProvider.GetTimestamp() + (long)(HandOnTimeout.TotalSeconds * _timeProvider.TimestampFrequency);

        while (!IsSettled() && _timeProvider.GetTimestamp() < deadline)
        {
            Thread.Sleep(_pollInterval);
        }

        if (!IsSettled())
        {
            _logger.LogWarning(
                "{LiveCalls} AI voice call(s) were still being handed on {Seconds} seconds into shutdown; their callers may hear silence until they hang up.",
                Math.Max(LiveCalls, _handOns.Count),
                HandOnTimeout.TotalSeconds);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _registration.Dispose();
        _stopping.Dispose();
    }

    private bool IsSettled()
        => LiveCalls == 0 && _handOns.IsEmpty;

    private sealed class HeldCall : IDisposable
    {
        private AIVoiceCallShutdown _owner;

        public HeldCall(AIVoiceCallShutdown owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);

            if (owner is not null)
            {
                Interlocked.Decrement(ref owner._liveCalls);
            }
        }
    }
}
