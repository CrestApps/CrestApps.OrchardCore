using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Runs one pacing cycle for each enabled dialer profile so power and progressive campaigns dial automatically.
/// <para>
/// Each run is bounded by a wall-clock budget enforced both by a between-profile deadline check and a hard
/// <see cref="System.Threading.CancellationTokenSource.CancelAfter(int)"/> that cancels in-flight work, and that
/// budget is kept safely below the distributed-lock expiration so a slow run can never outlive its lock and let a
/// second node begin an overlapping pacing cycle. Profiles that do not fit in the budget are simply paced on the
/// following tick.
/// </para>
/// </summary>
[BackgroundTask(
    Title = "Contact Center Dialer Pacing",
    Schedule = "* * * * *",
    Description = "Reserves agents and places outbound calls for enabled dialer profiles.",
    LockTimeout = 5_000,
    LockExpiration = LockExpirationMilliseconds)]
public sealed class DialerPacingBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The distributed-lock expiration, in milliseconds. Set to twice the one-minute schedule so the lock is not
    /// released while a run is still in progress.
    /// </summary>
    private const int LockExpirationMilliseconds = 120_000;

    /// <summary>
    /// The maximum wall-clock duration of a single run, in milliseconds. Kept safely below
    /// <see cref="LockExpirationMilliseconds"/> so the run always finishes before the lock can expire, which
    /// guarantees the next scheduled tick cannot start an overlapping pacing cycle on another node.
    /// </summary>
    private const int MaxRunDurationMilliseconds = 90_000;

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var workManager = serviceProvider.GetRequiredService<IContactCenterFeatureWorkManager>();
        using var workLease = workManager.TryEnter(ContactCenterConstants.Feature.DialerPaced);

        if (workLease is null)
        {
            return;
        }

        var dialerManager = serviceProvider.GetRequiredService<IDialerProfileManager>();
        var dialerService = serviceProvider.GetRequiredService<IDialerService>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<DialerPacingBackgroundTask>>();

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        runCts.CancelAfter(MaxRunDurationMilliseconds);
        var runToken = runCts.Token;

        var runDeadlineUtc = clock.UtcNow.AddMilliseconds(MaxRunDurationMilliseconds);

        IReadOnlyCollection<DialerProfile> profiles;

        try
        {
            profiles = await dialerManager.GetEnabledAsync(runToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (runToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while listing profiles; deferring to the next scheduled tick.",
                    MaxRunDurationMilliseconds);
            }

            return;
        }

        foreach (var profile in profiles)
        {
            if (clock.UtcNow >= runDeadlineUtc)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget; deferring the remaining profiles to the next scheduled tick.",
                        MaxRunDurationMilliseconds);
                }

                break;
            }

            try
            {
                await dialerService.RunCycleAsync(profile, runToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "The dialer pacing run reached its {BudgetMilliseconds} ms time budget while running profile '{Profile}'; deferring the remaining profiles to the next scheduled tick.",
                        MaxRunDurationMilliseconds,
                        profile.Name);
                }

                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while running dialer profile '{Profile}'.", profile.Name);
            }
        }
    }
}
