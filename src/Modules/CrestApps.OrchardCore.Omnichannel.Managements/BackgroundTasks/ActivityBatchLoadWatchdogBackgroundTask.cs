using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// Resumes activity loads that stopped recording progress, such as a load whose process was restarted part-way, and
/// gives up with a stated reason on a load that keeps stopping.
/// </summary>
/// <remarks>
/// A load runs in the background after the request that started it. When that process stopped, the load stopped
/// with it, and its batch stayed "Loading" for good: it could not be loaded again, removed, or told apart from a load
/// still running. Each page a load commits now records a heartbeat and the record it reached, so a load whose
/// heartbeat has gone quiet is dead, and is carried on from there.
/// </remarks>
[BackgroundTask(
    Title = "Omnichannel Activity Load Watchdog",
    Schedule = "* * * * *",
    Description = "Resumes activity loads that stopped part-way, for example because the server restarted.",
    LockTimeout = 5_000,
    LockExpiration = 1_800_000)]
public sealed class ActivityBatchLoadWatchdogBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// How long a load may go without committing progress before it is taken for dead. A page of a load commits well
    /// inside this, dialing included.
    /// </summary>
    internal static readonly TimeSpan StallThreshold = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many times a load is resumed before it is stopped with a reason, so a load that brings its process down
    /// every time is not restarted for ever.
    /// </summary>
    internal const int MaxResumes = 3;

    private static readonly OmnichannelActivityBatchStatus[] _runningStatuses =
    [
        OmnichannelActivityBatchStatus.Started,
        OmnichannelActivityBatchStatus.Loading,
    ];

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var session = serviceProvider.GetRequiredService<ISession>();
        var registry = serviceProvider.GetRequiredService<ActivityBatchLoadRegistry>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var logger = serviceProvider.GetRequiredService<ILogger<ActivityBatchLoadWatchdogBackgroundTask>>();

        var batches = await session.Query<OmnichannelActivityBatch, OmnichannelActivityBatchIndex>(
                index => index.Status.IsIn(_runningStatuses),
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var now = clock.UtcNow;

        // One load is resumed a run; any other stalled load is picked up on a following run.
        var stalled = batches
            .Where(batch => !registry.IsRunning(batch.ItemId) && IsStalled(batch, now))
            .OrderBy(batch => batch.LoadHeartbeatUtc ?? batch.LoadStartedUtc ?? DateTime.MinValue)
            .FirstOrDefault();

        if (stalled is null)
        {
            return;
        }

        // A load that was loading before loads recorded where they had got to cannot be carried on: started again from
        // the beginning, it would load every record it had already loaded a second time.
        if (stalled.Status == OmnichannelActivityBatchStatus.Loading && stalled.LoadCursor is null)
        {
            stalled.Status = OmnichannelActivityBatchStatus.New;
            stalled.LoadError = $"The load stopped part-way after loading {stalled.TotalLoaded ?? 0} activities, most likely because the server restarted, and it was started before loads could be resumed. Load it again with Prevent duplicates turned on, so the records already loaded are not loaded twice.";

            await session.SaveAsync(stalled, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "The activity load '{BatchId}' was left loading with no record of where it had got to; it was stopped so it can be loaded again.",
                stalled.ItemId);

            return;
        }

        if (stalled.LoadResumeCount >= MaxResumes)
        {
            stalled.Status = OmnichannelActivityBatchStatus.New;
            stalled.LoadError = $"The load stopped part-way {stalled.LoadResumeCount + 1} times and was not resumed again, after loading {stalled.TotalLoaded ?? 0} activities. Check the log for what stopped it, then load it again; turn on Prevent duplicates so the records already loaded are not loaded twice.";

            await session.SaveAsync(stalled, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "The activity load '{BatchId}' stopped part-way {Count} times and was given up.",
                stalled.ItemId,
                stalled.LoadResumeCount + 1);

            return;
        }

        stalled.LoadResumeCount++;
        stalled.LoadHeartbeatUtc = now;

        // Committed before the load runs again, so the count holds even if the resumed run brings the process down.
        await session.SaveAsync(stalled, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "The activity load '{BatchId}' stopped recording progress while {Status}; resuming it (resume {Count} of {Max}).",
            stalled.ItemId,
            stalled.Status,
            stalled.LoadResumeCount,
            MaxResumes);

        await serviceProvider.GetRequiredService<IActivityBatchLoadCoordinator>().ResumeAsync(stalled.ItemId, cancellationToken);
    }

    /// <summary>
    /// Gets whether a load has gone quiet for longer than a live load ever does. A load started before loads recorded
    /// their progress has no heartbeat at all, and its process has been restarted since, so it is taken for dead.
    /// </summary>
    /// <param name="batch">The batch being loaded.</param>
    /// <param name="utcNow">The current time.</param>
    internal static bool IsStalled(OmnichannelActivityBatch batch, DateTime utcNow)
    {
        var lastProgress = batch.LoadHeartbeatUtc ?? batch.LoadStartedUtc;

        return lastProgress is null || utcNow - lastProgress.Value > StallThreshold;
    }
}
