using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Assessments.Services;

/// <summary>
/// Submits attempts at their deadline: it claims the due deadline work from the leased queue, so a restart or a second
/// node never loses or doubles one, and sweeps for any attempt in progress whose deadline passed.
/// </summary>
[BackgroundTask(
    Title = "Learning attempt deadlines",
    Schedule = "* * * * *",
    Description = "Submits quiz attempts whose time is up.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class AttemptDeadlineBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var queue = serviceProvider.GetRequiredService<ILeasedWorkQueue<AttemptDeadlineWork>>();
            var attempts = serviceProvider.GetRequiredService<IAssessmentAttemptService>();

            foreach (var work in await queue.ClaimDueAsync(50, _lease, cancellationToken))
            {
                try
                {
                    await attempts.SubmitAtDeadlineAsync(work.AttemptId, cancellationToken);
                    await queue.CompleteAsync(work, cancellationToken: cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await queue.RetryAsync(work, exception.Message, LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10)), cancellationToken: cancellationToken);
                }
            }

            var clock = serviceProvider.GetRequiredService<IClock>();

            foreach (var attempt in await serviceProvider.GetRequiredService<AssessmentAttemptStore>().ListOverdueAsync(clock.UtcNow.AddMinutes(-2), 50, cancellationToken))
            {
                await attempts.SubmitAtDeadlineAsync(attempt.ItemId, cancellationToken);
            }
        });
    }
}
