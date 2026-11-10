using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Checks the AI alert rules that are due and notifies each change.
/// </summary>
[BackgroundTask(
    Title = "AI Alert Rules",
    Schedule = "* * * * *",
    Description = "Checks the AI alert rules and notifies when one triggers or resolves.",
    LockTimeout = 3_000,
    LockExpiration = 300_000)]
public sealed class AIAlertRuleBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => DoWorkCoreAsync(run, serviceProvider, cancellationToken));

    private static async Task DoWorkCoreAsync(MeasuredTaskRun run, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IAIAlertRuleStore>();
        var evaluator = serviceProvider.GetRequiredService<AIAlertRuleEvaluator>();
        var notifier = serviceProvider.GetRequiredService<AIAlertRuleNotifier>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIAlertRuleBackgroundTask>>();
        var now = clock.UtcNow;

        foreach (var rule in await store.ListEnabledAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (!AIAlertRuleEvaluator.IsDue(rule, now))
            {
                continue;
            }

            try
            {
                var change = await evaluator.CheckAsync(rule, now, cancellationToken);

                await store.UpdateAsync(rule, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);
                await notifier.NotifyAsync(rule, change);

                run.AddItems(1, "checked");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI alert rule '{RuleId}' could not be checked.", rule.ItemId.SanitizeLogValue());

                run.AddItems(1, "failed");
                run.ReportError(ex);
            }
        }

        if (run.Items.Count == 0)
        {
            // No rule was due.
            run.Skip();
        }
    }
}
