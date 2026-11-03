using System.Diagnostics;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Runs the queued AI test runs, a few per pass, each saved as soon as it finishes.
/// </summary>
[BackgroundTask(
    Title = "AI Test Runs",
    Schedule = "* * * * *",
    Description = "Runs the queued AI simulation tests.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class AITestRunBackgroundTask : IBackgroundTask
{
    /// <summary>
    /// The most runs one pass takes on.
    /// </summary>
    internal const int RunsPerPass = 10;

    private static readonly TimeSpan _passBudget = TimeSpan.FromMinutes(8);

    /// <inheritdoc/>
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var runStore = serviceProvider.GetRequiredService<IAITestRunStore>();
        var runner = serviceProvider.GetRequiredService<AITestRunner>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<AITestRunBackgroundTask>>();
        var elapsed = Stopwatch.StartNew();

        foreach (var run in await runStore.ListQueuedAsync(RunsPerPass, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested || elapsed.Elapsed > _passBudget)
            {
                break;
            }

            try
            {
                // Marked first and saved, so the list shows it running and a second node never takes it.
                run.Status = AITestRunStatus.Running;
                await runStore.UpdateAsync(run, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                await runner.RunAsync(run, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI test run '{RunId}' failed in the background.", run.ItemId.SanitizeLogValue());
            }
        }
    }
}
