using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Records how each run of a background task ended, what it processed and how close it came to its lock expiration.
/// Orchard Core already starts a span and records <c>orchardcore.background_task.duration</c> for every run (its count
/// is the number of runs), so this adds only what Orchard Core cannot see: <c>crestapps.background_task.outcomes</c>
/// for the runs that did not end <c>ok</c>, <c>crestapps.background_task.items</c>,
/// <c>crestapps.background_task.last_success</c> and <c>crestapps.background_task.lock.utilization</c>.
/// </summary>
/// <remarks>
/// Wrap the body of <see cref="IBackgroundTask.DoWorkAsync(IServiceProvider, CancellationToken)"/> with
/// <see cref="RunAsync(IBackgroundTask, Func{MeasuredTaskRun, Task})"/>. A task that logs and swallows a failure
/// calls <see cref="MeasuredTaskRun.ReportError(Exception)"/>, so the failure shows on the outcomes metric although
/// Orchard Core sees a successful run.
/// </remarks>
public static class BackgroundTaskTelemetry
{
    /// <summary>
    /// The run did not start its work because another node or worker holds the work lease.
    /// </summary>
    public const string OutcomeSkippedLease = "skipped_lease";

    /// <summary>
    /// The run stopped before it finished, because it used up the time or item budget it allows itself per run.
    /// </summary>
    public const string OutcomeBudgetExceeded = "budget_exceeded";

    private static readonly ConcurrentDictionary<Type, TimeSpan?> _lockExpirations = new();

    /// <summary>
    /// The number of background task runs that did not end <c>ok</c>, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TaskTag"/> and the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>:
    /// <c>error</c>, <c>skipped</c>, <see cref="OutcomeSkippedLease"/>, <c>cancelled</c>, <c>timeout</c> or
    /// <see cref="OutcomeBudgetExceeded"/>. Every run, <c>ok</c> ones included, is counted by Orchard Core's
    /// <c>orchardcore.background_task.duration</c>.
    /// </summary>
    public static Counter<long> Outcomes { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.background_task.outcomes",
        "{run}",
        "The number of background task runs that did not end ok, by how they ended.");

    /// <summary>
    /// The number of items background task runs processed, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TaskTag"/>
    /// and the <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>.
    /// </summary>
    public static Counter<long> Items { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateCounter<long>(
        "crestapps.background_task.items",
        "{item}",
        "The number of items background task runs processed, by what became of them.");

    /// <summary>
    /// The Unix time, in seconds, a background task last finished a run without an error, including a run that found
    /// nothing to do, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TaskTag"/>. Alert when it falls too far
    /// behind the current time.
    /// </summary>
    public static Gauge<long> LastSuccess { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateGauge<long>(
        "crestapps.background_task.last_success",
        "s",
        "The Unix time a background task last finished a run without an error.");

    /// <summary>
    /// The duration of a run divided by the lock expiration of its task, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TaskTag"/>. A value near or above 1 means the lock can expire while
    /// the run still works, so a second node may start the same task.
    /// </summary>
    public static Histogram<double> LockUtilization { get; } = CrestAppsOrchardCoreTelemetry.PlatformMeter.CreateHistogram<double>(
        "crestapps.background_task.lock.utilization",
        "1",
        "The duration of a background task run divided by the lock expiration of the task.",
        tags: null,
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 1, 1.5, 2],
        });

    /// <summary>
    /// Runs the work of a background task and records how it ended.
    /// </summary>
    /// <param name="task">The background task.</param>
    /// <param name="work">The work, which reports skips, items and swallowed errors on the <see cref="MeasuredTaskRun"/>.</param>
    /// <returns>A task that completes when the work completes. Exceptions are recorded and rethrown.</returns>
    public static Task RunAsync(this IBackgroundTask task, Func<MeasuredTaskRun, Task> work)
    {
        ArgumentNullException.ThrowIfNull(task);

        return RunAsync(task.GetType().Name, work, GetLockExpiration(task.GetType()));
    }

    /// <summary>
    /// Runs the work of a background task or a periodic job and records how it ended.
    /// </summary>
    /// <param name="taskName">The name of the task, by its type name.</param>
    /// <param name="work">The work, which reports skips, items and swallowed errors on the <see cref="MeasuredTaskRun"/>.</param>
    /// <param name="lockExpiration">The lock expiration of the task, or <see langword="null"/> when it holds no lock.</param>
    /// <returns>A task that completes when the work completes. Exceptions are recorded and rethrown.</returns>
    public static async Task RunAsync(string taskName, Func<MeasuredTaskRun, Task> work, TimeSpan? lockExpiration = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskName);
        ArgumentNullException.ThrowIfNull(work);

        var run = new MeasuredTaskRun(taskName);
        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            await work(run);
        }
        catch (Exception ex)
        {
            run.SetOutcomeFromException(ex);

            throw;
        }
        finally
        {
            Record(run, Stopwatch.GetElapsedTime(startTimestamp), lockExpiration);
        }
    }

    private static void Record(MeasuredTaskRun run, TimeSpan elapsed, TimeSpan? lockExpiration)
    {
        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.TaskTag, run.TaskName },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (run.Outcome != CrestAppsOrchardCoreTelemetry.OutcomeOk && Outcomes.Enabled)
        {
            var outcomeTags = tags;
            outcomeTags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, run.Outcome);
            Outcomes.Add(1, outcomeTags);
        }

        // A run that found nothing to do is healthy too; without it, an idle task would look stuck.
        if ((run.Outcome is CrestAppsOrchardCoreTelemetry.OutcomeOk or CrestAppsOrchardCoreTelemetry.OutcomeSkipped) && LastSuccess.Enabled)
        {
            LastSuccess.Record(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds(), tags);
        }

        if (lockExpiration is { } expiration && expiration > TimeSpan.Zero && LockUtilization.Enabled)
        {
            LockUtilization.Record(elapsed / expiration, tags);
        }

        if (Items.Enabled)
        {
            foreach (var (result, count) in run.Items)
            {
                var itemTags = tags;
                itemTags.Add(CrestAppsOrchardCoreTelemetry.ResultTag, result);
                Items.Add(count, itemTags);
            }
        }
    }

    private static TimeSpan? GetLockExpiration(Type taskType)
    {
        return _lockExpirations.GetOrAdd(taskType, static type =>
        {
            var attribute = type.GetCustomAttribute<BackgroundTaskAttribute>();

            return attribute is { LockExpiration: > 0 }
                ? TimeSpan.FromMilliseconds(attribute.LockExpiration)
                : null;
        });
    }
}
