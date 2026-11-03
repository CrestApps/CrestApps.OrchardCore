using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AI alert rule '{RuleId}' could not be checked.", rule.ItemId.SanitizeLogValue());
            }
        }
    }
}
