using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Runs the data a migration moves after the migration itself, as <see cref="ShellScope.AddDeferredTask(Func{ShellScope, Task})"/>
/// does, and measures it: a <c>crestapps.migration.run</c> span linked to the trace that ran the migration, the
/// <c>crestapps.migration.duration</c> histogram tagged with the migration's class name, and Information logs when
/// it starts and completes and an Error log when it fails. A data migration that walks every session or profile
/// can run for minutes after the tenant starts, and without these its progress is invisible.
/// </summary>
public static class MigrationTelemetry
{
    /// <summary>
    /// The tag that records the migration by its class name, such as <c>AIDeploymentIndexMigrations</c>.
    /// </summary>
    public const string MigrationTag = "migration";

    /// <summary>
    /// The name of the span of a deferred data migration.
    /// </summary>
    public const string SpanName = "crestapps.migration.run";

    /// <summary>
    /// The duration of the data a migration moves after it runs, tagged with <see cref="MigrationTag"/> and the outcome.
    /// </summary>
    public static readonly Histogram<double> Duration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        CrestAppsOrchardCoreTelemetry.PlatformMeter,
        "crestapps.migration.duration",
        "The duration of the data a migration moves after it runs.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Queues the data a migration moves to run after the current shell scope ends, and measures it.
    /// </summary>
    /// <param name="migration">The migration, by its class name, such as <c>nameof(AIDeploymentIndexMigrations)</c>.</param>
    /// <param name="work">The work, which receives the scope it runs in.</param>
    public static void Defer(string migration, Func<ShellScope, Task> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(migration);
        ArgumentNullException.ThrowIfNull(work);

        var traceParent = TraceContextPropagation.Capture();

        ShellScope.AddDeferredTask(scope => RunAsync(migration, traceParent, scope, work));
    }

    /// <summary>
    /// Runs the data a migration moves and measures it. <see cref="Defer(string, Func{ShellScope, Task})"/> calls it; call it directly for data a migration moves by other means.
    /// </summary>
    /// <param name="migration">The migration, by its class name.</param>
    /// <param name="traceParent">The <c>traceparent</c> captured when the migration ran, or <see langword="null"/>.</param>
    /// <param name="scope">The scope the work runs in.</param>
    /// <param name="work">The work.</param>
    /// <returns>A task that completes when the work completes. Exceptions are logged, recorded and rethrown.</returns>
    public static async Task RunAsync(string migration, string traceParent, ShellScope scope, Func<ShellScope, Task> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(migration);
        ArgumentNullException.ThrowIfNull(work);

        var logger = scope?.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(MigrationTelemetry)) ?? NullLogger.Instance;

        using var telemetry = TelemetryScope.StartLinked(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            SpanName,
            Duration,
            traceParent,
            new TagList
            {
                { MigrationTag, migration },
            });

        MigrationLog.Started(logger, migration);

        try
        {
            await work(scope);
            telemetry.Succeed();

            MigrationLog.Completed(logger, migration, (long)telemetry.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            MigrationLog.Failed(logger, ex, migration, (long)telemetry.Elapsed.TotalMilliseconds);

            throw;
        }
    }
}
