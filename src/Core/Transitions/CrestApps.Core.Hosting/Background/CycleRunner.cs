using CrestApps.Core.Locking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.Hosting.Background;

/// <summary>
/// Runs one <see cref="IBackgroundCycle"/> on a schedule.
/// </summary>
/// <remarks>
/// <para>
/// The framework default for a host that has no scheduler of its own. A host that does - a CMS with
/// its own background-task system, for instance - drives the same cycle from that and never registers
/// this.
/// </para>
/// <para>
/// Each pass takes the distributed lock named after the cycle, so a multi-node host does not run the
/// same sweep twice at once. A pass that cannot take the lock is skipped rather than queued, because
/// another node is already doing it and the work will not be due twice.
/// </para>
/// </remarks>
/// <typeparam name="TCycle">The cycle to run.</typeparam>
public sealed class CycleRunner<TCycle> : BackgroundService
    where TCycle : IBackgroundCycle
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedLockProvider _distributedLockProvider;
    private readonly IOptionsMonitor<BackgroundCycleOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CycleRunner{TCycle}"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to create a scope per pass.</param>
    /// <param name="distributedLockProvider">The lock that stops two nodes running the same pass.</param>
    /// <param name="options">The schedule, read per pass so a change takes effect without a restart.</param>
    /// <param name="timeProvider">The time provider used to wait between passes.</param>
    /// <param name="logger">The logger.</param>
    public CycleRunner(
        IServiceProvider serviceProvider,
        IDistributedLockProvider distributedLockProvider,
        IOptionsMonitor<BackgroundCycleOptions> options,
        TimeProvider timeProvider,
        ILogger<CycleRunner<TCycle>> logger)
    {
        _serviceProvider = serviceProvider;
        _distributedLockProvider = distributedLockProvider;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the name this cycle's options and lock are keyed by.
    /// </summary>
    public static string CycleName => typeof(TCycle).Name;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.Get(CycleName);

            try
            {
                await Task.Delay(options.Interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!options.Enabled)
            {
                continue;
            }

            await RunOnceAsync(options, stoppingToken);
        }
    }

    /// <summary>
    /// Runs one pass under the cycle's lock.
    /// </summary>
    /// <param name="options">The options for this pass.</param>
    /// <param name="stoppingToken">The cancellation token.</param>
    private async Task RunOnceAsync(BackgroundCycleOptions options, CancellationToken stoppingToken)
    {
        try
        {
            var (locker, locked) = await _distributedLockProvider.TryAcquireLockAsync(
                CycleName,
                options.LockTimeout,
                options.LockExpiration,
                stoppingToken);

            if (!locked)
            {
                return;
            }

            await using (locker)
            {
                // A scope per pass, so the cycle can hold scoped services and nothing it touches
                // leaks into the next pass.
                await using var scope = _serviceProvider.CreateAsyncScope();

                await scope.ServiceProvider.GetRequiredService<TCycle>().RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down, which is not a failure.
        }
        catch (Exception ex)
        {
            // Caught here rather than allowed to escape: an unhandled exception would end the loop,
            // and the sweep would stop running for the lifetime of the process with nothing to say so.
            _logger.LogError(ex, "The '{Cycle}' background cycle failed. It will run again on the next tick.", CycleName);
        }
    }
}
