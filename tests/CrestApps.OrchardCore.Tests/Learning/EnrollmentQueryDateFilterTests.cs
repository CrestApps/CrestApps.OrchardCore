using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The enrollment query's due and access-end filters and orders, which the staff AI tools list overdue and expiring
/// learners with, in the database rather than from a capped list.
/// </summary>
public sealed class EnrollmentQueryDateFilterTests
{
    [Fact]
    public async Task DueBefore_ListsActiveEnrollmentsPastTheirDueDate_SoonestDueFirst()
    {
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        courses.AddCourse("course1");
        var now = LearningTestHarness.Now;

        await EnrollAsync(harness, "late2", now.AddDays(60), now.AddDays(-1));
        await EnrollAsync(harness, "late1", now.AddDays(60), now.AddDays(-5));
        await EnrollAsync(harness, "future", now.AddDays(60), now.AddDays(5));
        await EnrollAsync(harness, "nodue", now.AddDays(60), null);

        var (count, items) = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().PageAsync(new EnrollmentQuery
        {
            Status = EnrollmentStatus.Active,
            DueBeforeUtc = now,
            Order = EnrollmentOrder.DueSoonest,
        }, 1, 10), commit: false);

        Assert.Equal(2, count);
        Assert.Equal(["late1", "late2"], items.Select(item => item.UserId));
    }

    [Fact]
    public async Task AccessEndsWithin_ListsActiveEnrollmentsWhoseAccessEndsInTheWindow_SoonestFirst()
    {
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        courses.AddCourse("course1");
        var now = LearningTestHarness.Now;

        await EnrollAsync(harness, "in10", now.AddDays(10), null);
        await EnrollAsync(harness, "in3", now.AddDays(3), null);
        await EnrollAsync(harness, "in40", now.AddDays(40), null);

        var (count, items) = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().PageAsync(new EnrollmentQuery
        {
            Status = EnrollmentStatus.Active,
            AccessEndsAfterUtc = now,
            AccessEndsBeforeUtc = now.AddDays(14),
            Order = EnrollmentOrder.AccessEndsSoonest,
        }, 1, 10), commit: false);

        Assert.Equal(2, count);
        Assert.Equal(["in3", "in10"], items.Select(item => item.UserId));
    }

    private static async Task EnrollAsync(CalendarTestHarness harness, string userId, DateTime accessEndsUtc, DateTime? dueUtc)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
            AccessEndsUtc = accessEndsUtc,
            DueUtc = dueUtc,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);
    }
}
