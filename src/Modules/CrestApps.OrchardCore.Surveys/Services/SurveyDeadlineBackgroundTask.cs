using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Submits responses at their deadline: it claims the due deadline work from the leased queue, so a restart or a second
/// node never loses or doubles one.
/// </summary>
[BackgroundTask(
    Title = "Survey deadlines",
    Schedule = "* * * * *",
    Description = "Submits timed survey responses whose time is up.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class SurveyDeadlineBackgroundTask : IBackgroundTask
{
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var queue = serviceProvider.GetRequiredService<ILeasedWorkQueue<SurveyDeadlineWork>>();
        var responses = serviceProvider.GetRequiredService<ISurveyResponseService>();

        foreach (var work in await queue.ClaimDueAsync(50, _lease, cancellationToken))
        {
            try
            {
                await responses.SubmitAtDeadlineAsync(work.ResponseId, cancellationToken);
                await queue.CompleteAsync(work, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await queue.RetryAsync(work, exception.Message, LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10)), cancellationToken: cancellationToken);
            }
        }
    }
}
