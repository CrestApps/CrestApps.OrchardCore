using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.Workflows;

/// <summary>
/// Raises the quiz attempt workflow events after the request, with the attempt's ids, score and actor as input.
/// </summary>
internal sealed class AssessmentsWorkflowObserver : IAttemptObserver
{
    public Task StartedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Trigger(nameof(AttemptStartedEvent), attempt, attempt.UserId);

    public Task SubmittedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Trigger(nameof(AttemptSubmittedEvent), attempt, attempt.UserId);

    public Task NeedsGradingAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Trigger(nameof(AttemptNeedsGradingEvent), attempt, attempt.UserId);

    public Task GradedAsync(AssessmentAttempt attempt, CancellationToken cancellationToken = default)
        => Trigger(nameof(AttemptGradedEvent), attempt, attempt.GradedBy ?? attempt.UserId);

    public Task ScoreChangedAsync(AssessmentAttempt attempt, AttemptScoreChange change, CancellationToken cancellationToken = default)
        => Trigger(nameof(AttemptScoreChangedEvent), attempt, change.Actor, input =>
        {
            input["Change"] = change.Kind;
            input["From"] = change.From;
            input["To"] = change.To;
            input["Reason"] = change.Reason;
        });

    private static Task Trigger(string name, AssessmentAttempt attempt, string actor, Action<Dictionary<string, object>> extra = null)
    {
        var input = new Dictionary<string, object>
        {
            ["AttemptId"] = attempt.ItemId,
            ["Number"] = attempt.Number,
            ["UserId"] = attempt.UserId,
            ["QuizId"] = attempt.QuizId,
            ["CourseId"] = attempt.CourseId,
            ["EnrollmentId"] = attempt.EnrollmentId,
            ["Status"] = attempt.Status.ToString(),
            ["Score"] = attempt.Score,
            ["Percent"] = attempt.Percent,
            ["Passed"] = attempt.Passed,
            ["AutoSubmitted"] = attempt.AutoSubmitted,
            ["SubmittedUtc"] = attempt.SubmittedUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["Actor"] = actor,
        };

        extra?.Invoke(input);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, attempt.ItemId));

        return Task.CompletedTask;
    }
}
