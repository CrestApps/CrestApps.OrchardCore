using CrestApps.Core.Support;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using YesSql;

namespace CrestApps.OrchardCore.AI.Services;

/// <summary>
/// Scores each enabled QA cohort's sample of the conversations that closed since its last pass, and raises the
/// <c>AI Conversation Evaluated</c> event for each score.
/// </summary>
[BackgroundTask(
    Title = "AI QA Cohorts",
    Schedule = "*/10 * * * *",
    Description = "Scores a sample of closed AI conversations for each QA cohort.",
    LockTimeout = 3_000,
    LockExpiration = 900_000)]
public sealed class AIQACohortBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => DoWorkCoreAsync(run, serviceProvider, cancellationToken));

    private static async Task DoWorkCoreAsync(MeasuredTaskRun run, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var store = serviceProvider.GetRequiredService<IAIQACohortStore>();
        var evaluator = serviceProvider.GetRequiredService<AIQACohortEvaluator>();
        var session = serviceProvider.GetRequiredService<ISession>();
        var logger = serviceProvider.GetRequiredService<ILogger<AIQACohortBackgroundTask>>();

        foreach (var cohort in await store.ListEnabledAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var evaluations = await evaluator.EvaluateNextAsync(cohort, cancellationToken);

                await store.UpdateAsync(cohort, cancellationToken);
                await session.SaveChangesAsync(cancellationToken);

                foreach (var evaluation in evaluations)
                {
                    await AIConversationEvaluatedEvent.TriggerAsync(serviceProvider, evaluation, logger);
                }

                run.AddItems(evaluations.Count, "evaluated");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "QA cohort '{CohortId}' could not be scored.", cohort.ItemId.SanitizeLogValue());

                run.AddItems(1, "cohort_failed");
                run.ReportError(ex);
            }
        }

        if (run.Items.Count == 0)
        {
            // No cohort had a closed conversation to score.
            run.Skip();
        }
    }
}
