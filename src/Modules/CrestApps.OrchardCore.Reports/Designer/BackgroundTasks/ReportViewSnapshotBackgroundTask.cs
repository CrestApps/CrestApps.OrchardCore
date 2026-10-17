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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var logger = serviceProvider.GetRequiredService<ILogger<ReportViewSnapshotBackgroundTask>>();
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

            return;
        }

        foreach (var viewId in due)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await InScopeAsync(serviceProvider, scoped => scoped
                    .GetRequiredService<ReportViewSnapshotRefresher>()
                    .RefreshAsync(viewId, onlyWhenDue: true, cancellationToken));
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(exception, "The report view '{ViewId}' could not be refreshed.", viewId);
                }
            }
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
