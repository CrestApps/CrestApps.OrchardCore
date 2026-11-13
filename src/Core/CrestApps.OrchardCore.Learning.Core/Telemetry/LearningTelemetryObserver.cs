using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Learning.Core.Telemetry;

/// <summary>
/// Counts enrollments, status changes, completed items, completed courses and notices on the Learning meter.
/// </summary>
internal sealed class LearningTelemetryObserver : IEnrollmentObserver
{
    public Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.Enrollments, LearningTelemetry.SourceTag, FirstSource(enrollment));

        return Task.CompletedTask;
    }

    public Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.AddStatusChange(previous.ToString(), enrollment.Status.ToString());

        return Task.CompletedTask;
    }

    public Task ItemCompletedAsync(Enrollment enrollment, CourseItemProgress item, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.ItemsCompleted, LearningTelemetry.CompletedByTag, string.IsNullOrEmpty(item.CompletedBy) ? "learner" : "staff");

        return Task.CompletedTask;
    }

    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        LearningTelemetry.Add(LearningTelemetry.CoursesCompleted, LearningTelemetry.SourceTag, FirstSource(enrollment));

        return Task.CompletedTask;
    }

    public Task AccessEndingAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Notice("access_ending");

    public Task DueSoonAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Notice("due_soon");

    public Task OverdueAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Notice("overdue");

    public Task InactiveAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Notice("inactive");

    private static string FirstSource(Enrollment enrollment)
        => enrollment.Grants.Count > 0 ? enrollment.Grants[0].Source : null;

    private static Task Notice(string kind)
    {
        LearningTelemetry.Add(LearningTelemetry.Notices, LearningTelemetry.NoticeTag, kind);

        return Task.CompletedTask;
    }
}
