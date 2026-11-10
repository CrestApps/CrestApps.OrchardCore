using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Queues work to run after the current shell scope ends, as <see cref="ShellScope.AddDeferredTask(Func{ShellScope, Task})"/>
/// does, and measures it: a <c>crestapps.deferred.&lt;name&gt;</c> span linked to the request that queued it, and
/// <c>crestapps.deferred_task.duration</c> by outcome (its count is the number of runs). A deferred task runs after the
/// response is sent, so without it its failure is only a log line.
/// </summary>
public static class DeferredWork
{
    /// <summary>
    /// The duration of deferred task runs, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TaskTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> Duration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        CrestAppsOrchardCoreTelemetry.PlatformMeter,
        "crestapps.deferred_task.duration",
        "The duration of deferred tasks run after a shell scope ended.");

    /// <summary>
    /// Queues work to run after the current shell scope ends, and measures it.
    /// </summary>
    /// <param name="name">The name of the work, in snake_case from a bounded set, such as <c>ai_datasource_indexing</c>.</param>
    /// <param name="work">The work, which receives the scope it runs in.</param>
    public static void Add(string name, Func<ShellScope, Task> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(work);

        var traceParent = TraceContextPropagation.Capture();

        ShellScope.AddDeferredTask(scope => RunAsync(name, traceParent, scope, work));
    }

    /// <summary>
    /// Runs deferred work and measures it. <see cref="Add(string, Func{ShellScope, Task})"/> calls it; call it directly
    /// for work that is deferred by other means, such as a <c>ShellScope.UsingChildScopeAsync</c> call queued elsewhere.
    /// </summary>
    /// <param name="name">The name of the work, in snake_case from a bounded set.</param>
    /// <param name="traceParent">The <c>traceparent</c> captured when the work was queued, or <see langword="null"/>.</param>
    /// <param name="scope">The scope the work runs in.</param>
    /// <param name="work">The work.</param>
    /// <returns>A task that completes when the work completes. Exceptions are recorded and rethrown, so Orchard Core
    /// still logs them.</returns>
    public static async Task RunAsync(string name, string traceParent, ShellScope scope, Func<ShellScope, Task> work)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(work);

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.TaskTag, name },
        };

        using var telemetry = TelemetryScope.StartLinked(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.deferred." + name,
            Duration,
            traceParent,
            tags);

        try
        {
            await work(scope);
            telemetry.Succeed();
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }
}
