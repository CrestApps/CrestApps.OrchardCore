using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Records what the enrollment observers hear, as "Event:enrollmentId" strings.
/// </summary>
internal sealed class RecordingEnrollmentObserver : IEnrollmentObserver
{
    public List<string> Events { get; } = [];

    public Task RequestedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Requested", enrollment);

    public Task EnrolledAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Enrolled", enrollment);

    public Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
        => Record($"Status:{previous}->{enrollment.Status}", enrollment);

    public Task CourseStartedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Started", enrollment);

    public Task ItemCompletedAsync(Enrollment enrollment, CourseItemProgress item, CancellationToken cancellationToken = default)
        => Record("ItemCompleted:" + item.ContentItemId, enrollment);

    public Task CompletedAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Completed", enrollment);

    public Task ExtendedAsync(Enrollment enrollment, string actor, string reason, CancellationToken cancellationToken = default)
        => Record("Extended", enrollment);

    public Task AccessEndingAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("AccessEnding:" + enrollment.ItemId, enrollment);

    public Task InactiveAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Inactive:" + enrollment.ItemId, enrollment);

    public Task ReviewNeededAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("ReviewNeeded", enrollment);

    public Task DueSoonAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("DueSoon:" + enrollment.ItemId, enrollment);

    public Task OverdueAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
        => Record("Overdue:" + enrollment.ItemId, enrollment);

    private Task Record(string name, Enrollment enrollment)
    {
        Events.Add(name);

        return Task.CompletedTask;
    }
}
