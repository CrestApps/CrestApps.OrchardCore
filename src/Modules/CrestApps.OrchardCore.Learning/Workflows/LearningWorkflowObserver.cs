using System.Globalization;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// Starts the Learning workflow events after the change is committed, with the enrollment id as the correlation id.
/// </summary>
internal sealed class LearningWorkflowObserver : IEnrollmentObserver
{
    public Task RequestedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(EnrollmentRequestedEvent), enrollment);

    public Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(LearnerEnrolledEvent), enrollment);

    public Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
        => Trigger(nameof(EnrollmentStatusChangedEvent), enrollment, input => input["PreviousStatus"] = previous.ToString());

    public Task CourseStartedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(CourseStartedEvent), enrollment);

    public Task ItemCompletedAsync(Enrollment enrollment, CourseItemProgress item, CancellationToken cancellationToken = default)
        => Trigger(nameof(CourseItemCompletedEvent), enrollment, input =>
        {
            input["ItemId"] = item.ContentItemId;
            input["Score"] = item.Score;
            input["SelfReported"] = item.SelfReported;
        });

    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(CourseCompletedEvent), enrollment, input =>
        {
            input["Grade"] = enrollment.Grade;
            input["Passed"] = enrollment.Passed;
            input["CompletedUtc"] = enrollment.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
        });

    public Task AccessEndingAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(AccessExpiringEvent), enrollment);

    public Task InactiveAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Trigger(nameof(LearnerInactiveEvent), enrollment);

    private static Task Trigger(string name, Enrollment enrollment, Action<Dictionary<string, object>> extra = null)
    {
        var input = new Dictionary<string, object>
        {
            ["EnrollmentId"] = enrollment.ItemId,
            ["Number"] = enrollment.Number,
            ["UserId"] = enrollment.UserId,
            ["CourseId"] = enrollment.CourseId,
            ["OfferingId"] = enrollment.OfferingId,
            ["Status"] = enrollment.Status.ToString(),
            ["Cycle"] = enrollment.Cycle,
            ["ProgressPercent"] = enrollment.ProgressPercent,
            ["AccessEndsUtc"] = enrollment.AccessEndsUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["DueUtc"] = enrollment.DueUtc?.ToString("O", CultureInfo.InvariantCulture),
            ["Actor"] = enrollment.History.Count > 0 ? enrollment.History[enrollment.History.Count - 1].Actor : null,
        };

        extra?.Invoke(input);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, enrollment.ItemId));

        return Task.CompletedTask;
    }
}
