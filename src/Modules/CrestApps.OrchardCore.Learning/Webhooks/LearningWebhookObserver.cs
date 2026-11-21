using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Learning.Webhooks;

/// <summary>
/// Publishes the Learning webhook events after the change is committed. Event ids come from the enrollment, so a
/// receiver can tell a repeated delivery apart.
/// </summary>
internal sealed class LearningWebhookObserver : IEnrollmentObserver
{
    /// <summary>
    /// The event raised when a learner is enrolled.
    /// </summary>
    public const string EnrollmentCreated = "learning.enrollment.created";

    /// <summary>
    /// The event raised when a learner completes a course.
    /// </summary>
    public const string CourseCompleted = "learning.course.completed";

    public Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Publish(EnrollmentCreated, enrollment.ItemId + ":enrolled", enrollment);

    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Publish(CourseCompleted, enrollment.ItemId + ":completed", enrollment);

    private static Task Publish(string eventName, string eventId, Enrollment enrollment)
    {
        var payload = new
        {
            enrollmentId = enrollment.ItemId,
            number = enrollment.Number,
            userId = enrollment.UserId,
            courseId = enrollment.CourseId,
            offeringId = enrollment.OfferingId,
            status = enrollment.Status.ToString(),
            cycle = enrollment.Cycle,
            grade = enrollment.Grade,
            passed = enrollment.Passed,
            completedUtc = enrollment.CompletedUtc,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWebhookPublisher>().PublishAsync(eventName, payload, eventId));

        return Task.CompletedTask;
    }
}
