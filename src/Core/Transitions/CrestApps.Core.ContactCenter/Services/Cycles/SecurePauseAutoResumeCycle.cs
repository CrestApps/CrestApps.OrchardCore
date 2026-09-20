using CrestApps.Core.Hosting.Background;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.ContactCenter.Services;

/// <summary>
/// Force-resumes recordings that have stayed paused past the tenant's maximum secure-pause window, so a
/// sensitive-data pause that was never explicitly resumed cannot silently suppress capture for the remainder of a
/// compliance-recorded call.
/// <para>
/// The pass is bounded by a hard wall-clock budget enforced through a linked
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that stays safely below the
/// distributed-lock expiration, so a slow pass cannot outlive its lock and let a second node begin an overlapping
/// resume pass. Work that does not fit in the budget is resumed on the following tick.
/// </para>
/// </summary>
public sealed class SecurePauseAutoResumeCycle : ISecurePauseAutoResumeCycle
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    private readonly ISecurePauseAutoResumeService _autoResumeService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurePauseAutoResumeCycle"/> class.
    /// </summary>
    /// <param name="autoResumeService">The auto resume service.</param>
    /// <param name="logger">The logger.</param>
    public SecurePauseAutoResumeCycle(
        ISecurePauseAutoResumeService autoResumeService,
        ILogger<SecurePauseAutoResumeCycle> logger)
    {
        _autoResumeService = autoResumeService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        try
        {
            var resumed = await _autoResumeService.ResumeExpiredAsync(runToken);

            if (resumed > 0 && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Force-resumed {Count} recording(s) paused past the maximum secure-pause window.", resumed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "The secure-pause auto-resume run reached its {BudgetMilliseconds} ms time budget; deferring the remaining work to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while resuming recordings paused past the maximum secure-pause window.");
        }
    }
}
