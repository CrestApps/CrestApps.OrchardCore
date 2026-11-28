using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Submits responses at their deadline: it claims the due deadline work from the leased queue, so a restart or a second
/// node never loses or doubles one. Each response is submitted in its own child shell scope, with a session of its own
/// that commits on its own: a response whose save fails never takes the others with it, and its work item stays in the
/// queue to be tried again. A response that is busy (the respondent's own save holds it) is tried again shortly; only a
/// response that was submitted, or no longer needs to be, settles its work item.
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
    private static readonly TimeSpan _busyDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var queue = serviceProvider.GetRequiredService<ILeasedWorkQueue<SurveyDeadlineWork>>();
        var logger = serviceProvider.GetRequiredService<ILogger<SurveyDeadlineBackgroundTask>>();

        foreach (var work in await queue.ClaimDueAsync(50, _lease, cancellationToken))
        {
            SurveyResponseResult result = null;

            try
            {
                await ShellScope.UsingChildScopeAsync(async scope =>
                {
                    result = await scope.ServiceProvider.GetRequiredService<ISurveyResponseService>().SubmitAtDeadlineAsync(work.ResponseId, cancellationToken);

                    if (result.Error != SurveyErrorCode.Busy)
                    {
                        // Writes the submission's observers made after its commit (the audit trail, for one).
                        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
                    }
                });
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "The survey response '{ResponseId}' could not be submitted at its deadline. It is tried again later.", work.ResponseId);

                await queue.RetryAsync(work, exception.Message, LeasedWorkBackoff.Exponential(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10)), cancellationToken: cancellationToken);

                continue;
            }

            if (result?.Error == SurveyErrorCode.Busy)
            {
                // Nothing was submitted: the respondent's own save held the response. It is tried again shortly, without
                // using up an attempt, instead of waiting for the hourly upkeep.
                await queue.RetryAsync(work, "The response was busy.", _ => _busyDelay, countAttempt: false, cancellationToken: cancellationToken);

                continue;
            }

            await queue.CompleteAsync(work, cancellationToken: cancellationToken);
        }
    }
}
