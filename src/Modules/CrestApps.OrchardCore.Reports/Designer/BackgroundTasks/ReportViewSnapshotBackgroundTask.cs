using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Reports.Designer.BackgroundTasks;

/// <summary>
/// Refreshes the stored result of every scheduled report view that is due. Each view is refreshed in its own shell
/// scope, so a view that fails, even in a way that breaks its database session, never stops the others.
/// </summary>
[BackgroundTask(
    Title = "Report view refresh",
    Schedule = "*/5 * * * *",
    Description = "Refreshes the stored results of scheduled report views that are due.",
    LockTimeout = 5_000,
    LockExpiration = 3_600_000)]
public sealed class ReportViewSnapshotBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// Refreshes the due views.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return this.RunAsync(run => SweepAsync(serviceProvider, run, cancellationToken));
    }

    private static async Task SweepAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ReportViewSnapshotBackgroundTask>>();
        using var activity = ReportTelemetry.Start("report.view.sweep");
        IReadOnlyList<string> due;

        try
        {
            due = await serviceProvider.GetRequiredService<ReportViewSnapshotRefresher>().ListDueAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(exception, "The report views that are due for a refresh could not be listed.");
            }

            CrestAppsOrchardCoreTelemetry.RecordException(activity, exception);
            run.ReportError(exception);

            return;
        }

        activity?.SetTag("crestapps.report.views.due", due.Count);

        if (due.Count == 0)
        {
            run.Skip();

            return;
        }

        var refreshed = 0;
        var failed = 0;
        var skipped = 0;

        try
        {
            foreach (var viewId in due)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    ReportViewRefreshStatus? status = null;

                    using (logger.BeginScope(new Dictionary<string, object> { ["ReportViewId"] = viewId }))
                    {
                        await InScopeAsync(serviceProvider, async scoped => status = (await scoped
                            .GetRequiredService<ReportViewSnapshotRefresher>()
                            .RefreshAsync(viewId, onlyWhenDue: true, cancellationToken)).Status);
                    }

                    switch (status)
                    {
                        case ReportViewRefreshStatus.Refreshed:
                            refreshed++;
                            break;

                        case ReportViewRefreshStatus.Failed:
                            failed++;
                            break;

                        default:
                            skipped++;
                            break;
                    }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    failed++;

                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(exception, "The report view '{ViewId}' could not be refreshed.", viewId);
                    }
                }
            }
        }
        finally
        {
            run.AddItems(refreshed, "refreshed");
            run.AddItems(failed, "failed");
            run.AddItems(skipped, "skipped");
            activity?.SetTag("crestapps.report.views.refreshed", refreshed);
            activity?.SetTag("crestapps.report.views.failed", failed);
            activity?.SetTag("crestapps.report.views.skipped", skipped);
            ReportsModuleLog.ViewSweepCompleted(logger, due.Count, refreshed, failed, skipped);
        }
    }

    private static async Task InScopeAsync(IServiceProvider serviceProvider, Func<IServiceProvider, Task> operation)
    {
        if (ShellScope.Current is null)
        {
            await using var scope = serviceProvider.CreateAsyncScope();

            await operation(scope.ServiceProvider);

            return;
        }

        await ShellScope.UsingChildScopeAsync(scope => operation(scope.ServiceProvider));
    }
}
