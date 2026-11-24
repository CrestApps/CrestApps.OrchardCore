using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A course's start and end dates: before the start, learners see the course but cannot open its items; the end date
/// never locks the course, as in Moodle; the access length after enrollment keeps working beside both.
/// </summary>
public sealed class CourseDatesTests
{
    [Fact]
    public async Task BeforeTheStart_ALearnerCannotOpenAnItem_AndIsToldWhenTheCourseStarts()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        var starts = LearningTestHarness.Now.AddDays(3);
        courses.AddCourse("course1", part => part.StartsUtc = starts);
        var lesson = courses.AddItem("course1", "lesson");
        var preview = courses.AddItem("course1", "preview", configure: part => part.FreePreview = true);
        await EnrollAsync(harness, "learner", "course1");

        // Act
        var learner = await AccessAsync(harness, CalendarTestHarness.User("learner"), lesson);
        var author = await AccessAsync(harness, CalendarTestHarness.User("author", LearningPermissions.ManageCourses.Name), lesson);
        var anonymousPreview = await AccessAsync(harness, new ClaimsPrincipal(new ClaimsIdentity()), preview);
        var learnerPreview = await AccessAsync(harness, CalendarTestHarness.User("learner"), preview);

        // Assert
        Assert.False(learner.Allowed);
        Assert.Equal(CourseItemAccessReason.NotReleased, learner.Reason);
        Assert.Equal(LearningRefusals.CourseNotStarted, learner.LockedReason);
        Assert.Equal(starts, learner.AvailableFromUtc);
        Assert.NotNull(learner.Enrollment);
        Assert.Equal(CourseItemAccessReason.Staff, author.Reason);
        Assert.Equal(CourseItemAccessReason.FreePreview, anonymousPreview.Reason);
        Assert.True(learnerPreview.Allowed);
    }

    [Fact]
    public async Task OnceTheCourseHasStarted_TheItemsOpen()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.StartsUtc = LearningTestHarness.Now.AddMinutes(-1));
        var lesson = courses.AddItem("course1", "lesson");
        await EnrollAsync(harness, "learner", "course1");

        // Act
        var access = await AccessAsync(harness, CalendarTestHarness.User("learner"), lesson);

        // Assert
        Assert.Equal(CourseItemAccessReason.Enrolled, access.Reason);
    }

    [Fact]
    public async Task AfterTheEnd_TheCourseStaysOpen_AsMoodleKeepsItOpen()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part =>
        {
            part.StartsUtc = LearningTestHarness.Now.AddYears(-2);
            part.EndsUtc = LearningTestHarness.Now.AddYears(-1);
        });
        var lesson = courses.AddItem("course1", "lesson");
        await EnrollAsync(harness, "learner", "course1");

        // Act
        var access = await AccessAsync(harness, CalendarTestHarness.User("learner"), lesson);

        // Assert
        Assert.Equal(CourseItemAccessReason.Enrolled, access.Reason);
    }

    [Fact]
    public async Task TheAccessLengthAfterEnrollment_StillEndsAccess_InACourseThatHasStarted()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part =>
        {
            part.StartsUtc = LearningTestHarness.Now.AddDays(-10);
            part.EndsUtc = LearningTestHarness.Now.AddDays(10);
        });
        var lesson = courses.AddItem("course1", "lesson");
        await EnrollAsync(harness, "ended", "course1", LearningTestHarness.Now.AddMinutes(-5));

        // Act
        var access = await AccessAsync(harness, CalendarTestHarness.User("ended"), lesson);

        // Assert
        Assert.Equal(CourseItemAccessReason.AccessEnded, access.Reason);
    }

    [Theory]
    [InlineData(-5, null, CourseSchedule.Open)]
    [InlineData(5, null, CourseSchedule.NotStarted)]
    [InlineData(null, 5, CourseSchedule.Open)]
    [InlineData(null, -5, CourseSchedule.Ended)]
    [InlineData(-10, -5, CourseSchedule.Ended)]
    [InlineData(null, null, CourseSchedule.Open)]
    public void CourseSchedule_TellsWhetherACourseHasStartedOrEnded(int? startsInDays, int? endsInDays, CourseSchedule expected)
    {
        // Arrange
        var now = LearningTestHarness.Now;
        var part = new CoursePart
        {
            StartsUtc = startsInDays is { } starts ? now.AddDays(starts) : null,
            EndsUtc = endsInDays is { } ends ? now.AddDays(ends) : null,
        };

        // Act
        var schedule = CourseSchedules.Of(part, now);

        // Assert
        Assert.Equal(expected, schedule);
    }

    private static Task<CourseItemAccess> AccessAsync(CalendarTestHarness harness, ClaimsPrincipal user, ContentItem item)
        => harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(user, item), commit: false);

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string courseId, DateTime? accessEndsUtc = null)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
            AccessEndsUtc = accessEndsUtc,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }
}
