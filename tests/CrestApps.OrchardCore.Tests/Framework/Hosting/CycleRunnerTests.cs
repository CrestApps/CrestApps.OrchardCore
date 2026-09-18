using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Hosting.Locking;
using CrestApps.Core.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CrestApps.OrchardCore.Tests.Framework.Hosting;

/// <summary>
/// Pins the framework's background-cycle runner.
/// </summary>
/// <remarks>
/// No Orchard Core startup registers this - Orchard drives the same cycles from its own background-task
/// system - so nothing else here exercises it. It becomes the only thing running the suite's sweeps in a
/// standalone host, and a sweep that silently stops running is how reservations never expire, outbound
/// messages never leave the outbox, and nobody finds out until a customer does.
/// </remarks>
public sealed class CycleRunnerTests
{
    [Fact]
    public async Task TheCycle_RunsOnceAnInterval()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.StartAsync();
        await harness.TickAsync();
        await harness.TickAsync();

        // Assert
        Assert.Equal(2, harness.Cycle.Runs);

        await harness.StopAsync();
    }

    [Fact]
    public async Task EachPass_GetsItsOwnScope()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await harness.StartAsync();
        await harness.TickAsync();
        await harness.TickAsync();

        // Assert: a shared scope would carry one pass's unit of work into the next.
        Assert.Equal(2, harness.ScopesCreated);

        await harness.StopAsync();
    }

    [Fact]
    public async Task WhenTheCycleIsDisabled_NothingRuns()
    {
        // Arrange
        var harness = new Harness();
        harness.Options.Enabled = false;

        // Act
        await harness.StartAsync();
        await harness.TickAsync();

        // Assert
        Assert.Equal(0, harness.Cycle.Runs);

        await harness.StopAsync();
    }

    [Fact]
    public async Task WhenAnotherNodeHoldsTheLock_ThePassIsSkipped()
    {
        // Arrange
        var harness = new Harness();

        // Another node is already doing this pass.
        var (held, _) = await harness.Locks.TryAcquireLockAsync(
            CycleRunner<ICountingCycle>.CycleName,
            TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.StartAsync();
        await harness.TickAsync();

        // Assert: skipped rather than queued. The work is not due twice because two nodes noticed it.
        Assert.Equal(0, harness.Cycle.Runs);

        await held.DisposeAsync();
        await harness.StopAsync();
    }

    [Fact]
    public async Task WhenAPassThrows_TheNextOneStillRuns()
    {
        // Arrange
        var harness = new Harness();
        harness.Cycle.ThrowOnRun = 1;

        // Act
        await harness.StartAsync();
        await harness.TickAsync();
        await harness.TickAsync();

        // Assert: an exception escaping the loop would stop the sweep for the life of the process,
        // with nothing to say it had.
        Assert.Equal(2, harness.Cycle.Runs);

        await harness.StopAsync();
    }

    [Fact]
    public async Task AFailedPass_ReleasesTheLock()
    {
        // Arrange
        var harness = new Harness();
        harness.Cycle.ThrowOnRun = 1;

        // Act
        await harness.StartAsync();
        await harness.TickAsync();

        // Assert: a lock held by a failed pass would block every node until it expired.
        Assert.False(await harness.Locks.IsLockAcquiredAsync(
            CycleRunner<ICountingCycle>.CycleName,
            TestContext.Current.CancellationToken));

        await harness.StopAsync();
    }

    [Fact]
    public void TheCycleName_IsTheContractsName()
        => Assert.Equal(nameof(ICountingCycle), CycleRunner<ICountingCycle>.CycleName);

    /// <summary>
    /// A cycle that counts its passes.
    /// </summary>
    internal interface ICountingCycle : IBackgroundCycle;

    private sealed class Harness
    {
        private readonly CycleRunner<ICountingCycle> _runner;
        private readonly FakeTimeProvider _timeProvider = new();

        private CancellationTokenSource _stopping;
        private Task _running;

        public Harness()
        {
            var services = new ServiceCollection();
            services.AddScoped<ICountingCycle>(_ => Cycle);

            var provider = new ScopeCountingServiceProvider(services.BuildServiceProvider(), this);

            Locks = new LocalDistributedLockProvider(_timeProvider);

            _runner = new CycleRunner<ICountingCycle>(
                provider,
                Locks,
                new NamedOptionsMonitor(Options),
                _timeProvider,
                NullLogger<CycleRunner<ICountingCycle>>.Instance);
        }

        public CountingCycle Cycle { get; } = new();

        public LocalDistributedLockProvider Locks { get; }

        public BackgroundCycleOptions Options { get; } = new()
        {
            Interval = TimeSpan.FromMinutes(1),
            LockTimeout = TimeSpan.Zero,
        };

        public int ScopesCreated { get; private set; }

        public void CountScope() => ScopesCreated++;

        public async Task StartAsync()
        {
            _stopping = new CancellationTokenSource();
            _running = _runner.StartAsync(_stopping.Token);

            await _running;
        }

        /// <summary>
        /// Advances past one interval and lets the pass finish.
        /// </summary>
        /// <remarks>
        /// Advanced repeatedly rather than once, because the runner is only waiting on a timer between
        /// passes: an advance that lands while it is still running the previous pass reaches no timer
        /// at all and is simply lost.
        /// </remarks>
        public async Task TickAsync()
        {
            var before = ObservedPasses;

            for (var attempt = 0; attempt < 400 && ObservedPasses == before; attempt++)
            {
                _timeProvider.Advance(Options.Interval);

                await Task.Yield();
                await Task.Delay(5, TestContext.Current.CancellationToken);

                if (!ExpectsAPass)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Gets what a completed pass is counted by. A pass the runner is expected to skip leaves this
        /// unchanged, so the tick gives up rather than spinning.
        /// </summary>
        private int ObservedPasses => Cycle.Runs;

        /// <summary>
        /// Gets whether a tick is expected to produce a pass at all.
        /// </summary>
        private bool ExpectsAPass => Options.Enabled;

        public async Task StopAsync()
        {
            await _stopping.CancelAsync();

            try
            {
                await _runner.StopAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }

            _stopping.Dispose();
        }
    }

    private sealed class CountingCycle : ICountingCycle
    {
        public int Runs { get; private set; }

        public int ThrowOnRun { get; set; }

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            Runs++;

            if (Runs == ThrowOnRun)
            {
                throw new InvalidOperationException("the store is unreachable");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Counts the scopes the runner creates, which is what proves each pass gets its own.
    /// </summary>
    private sealed class ScopeCountingServiceProvider : IServiceProvider, IServiceScopeFactory
    {
        private readonly ServiceProvider _inner;
        private readonly Harness _harness;

        public ScopeCountingServiceProvider(ServiceProvider inner, Harness harness)
        {
            _inner = inner;
            _harness = harness;
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(IServiceScopeFactory) ? this : _inner.GetService(serviceType);

        public IServiceScope CreateScope()
        {
            _harness.CountScope();

            return _inner.CreateScope();
        }
    }

    private sealed class NamedOptionsMonitor : IOptionsMonitor<BackgroundCycleOptions>
    {
        private readonly BackgroundCycleOptions _options;

        public NamedOptionsMonitor(BackgroundCycleOptions options)
            => _options = options;

        public BackgroundCycleOptions CurrentValue => _options;

        public BackgroundCycleOptions Get(string name) => _options;

        public IDisposable OnChange(Action<BackgroundCycleOptions, string> listener) => null;
    }
}
