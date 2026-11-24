using System.Globalization;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Coursework.Workflows;

/// <summary>
/// Raises the coursework workflow events after the request, with the submission's ids, grade and actor as input. The
/// submission is the correlation id.
/// </summary>
internal sealed class CourseworkWorkflowObserver : ISubmissionObserver
{
    public Task SubmittedAsync(Submission submission, CancellationToken cancellationToken = default)
        => Trigger(nameof(CourseworkSubmittedEvent), submission, submission.UserId);

    public Task GradedAsync(Submission submission, SubmissionGrade previous, CancellationToken cancellationToken = default)
        => Trigger(nameof(CourseworkGradedEvent), submission, submission.GraderId, input =>
        {
            input["Regraded"] = previous is not null;
            input["PreviousScore"] = previous?.Score;
            input["Reason"] = previous?.Reason;
        });

    private static Task Trigger(string name, Submission submission, string actor, Action<Dictionary<string, object>> extra = null)
    {
        var input = new Dictionary<string, object>
        {
            ["SubmissionId"] = submission.ItemId,
            ["UserId"] = submission.UserId,
            ["CourseworkId"] = submission.CourseworkId,
            ["CourseId"] = submission.CourseId,
            ["EnrollmentId"] = submission.EnrollmentId,
            ["Attempt"] = submission.Attempt,
            ["Status"] = submission.Status.ToString(),
            ["Late"] = submission.Late,
            ["Score"] = submission.Score,
            ["Points"] = submission.Points,
            ["MaxPoints"] = submission.MaxPoints,
            ["SubmittedUtc"] = submission.SubmittedUtc.ToString("O", CultureInfo.InvariantCulture),
            ["Actor"] = actor,
        };

        extra?.Invoke(input);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, submission.ItemId));

        return Task.CompletedTask;
    }
}
