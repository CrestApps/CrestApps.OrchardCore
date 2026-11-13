using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class CourseAccessTests
{
    [Fact]
    public async Task GetItemAccessAsync_DecidesForEachKindOfPerson()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
        var lesson = courses.AddItem("course1", "lesson");
        var preview = courses.AddItem("course1", "preview", configure: part => part.FreePreview = true);
        var later = courses.AddItem("course1", "later", configure: part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "lesson" } },
        ]);
        await EnrollAsync(harness, "learner", "course1");
        var pending = await EnrollAsync(harness, "waiting", "course1", requiresApproval: true);
        var suspended = await EnrollAsync(harness, "paused", "course1");
        await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(suspended.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));
        await EnrollAsync(harness, "ended", "course1", LearningTestHarness.Now.AddMinutes(-5));

        // Act
        Task<CourseItemAccess> Access(ClaimsPrincipal user, ContentItem item)
            => harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(user, item), commit: false);

        // Assert
        Assert.Equal(CourseItemAccessReason.Staff, (await Access(CalendarTestHarness.User("author", LearningPermissions.ManageCourses.Name), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.Staff, (await Access(CalendarTestHarness.User("teacher", LearningPermissions.ManageOwnCourses.Name), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.NotEnrolled, (await Access(CalendarTestHarness.User("other-teacher", LearningPermissions.ManageOwnCourses.Name), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.FreePreview, (await Access(new ClaimsPrincipal(new ClaimsIdentity()), preview)).Reason);
        Assert.Equal(CourseItemAccessReason.Anonymous, (await Access(new ClaimsPrincipal(new ClaimsIdentity()), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.Enrolled, (await Access(CalendarTestHarness.User("learner"), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.NotReleased, (await Access(CalendarTestHarness.User("learner"), later)).Reason);
        Assert.Equal(CourseItemAccessReason.NotEnrolled, (await Access(CalendarTestHarness.User("stranger"), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.Pending, (await Access(CalendarTestHarness.User("waiting"), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.Suspended, (await Access(CalendarTestHarness.User("paused"), lesson)).Reason);
        Assert.Equal(CourseItemAccessReason.AccessEnded, (await Access(CalendarTestHarness.User("ended"), lesson)).Reason);
        Assert.Equal(pending.ItemId, (await Access(CalendarTestHarness.User("waiting"), lesson)).Enrollment.ItemId);
    }

    [Fact]
    public async Task GetItemAccessAsync_OnAFreePreview_TreatsAnEnrolledLearnerAsEnrolled_SoProgressCounts()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var preview = courses.AddItem("course1", "preview", configure: part => part.FreePreview = true);
        var enrollment = await EnrollAsync(harness, "learner", "course1");

        // Act
        var access = await harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(CalendarTestHarness.User("learner"), preview), commit: false);
        var stranger = await harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(CalendarTestHarness.User("stranger"), preview), commit: false);

        // Assert
        Assert.Equal(CourseItemAccessReason.Enrolled, access.Reason);
        Assert.Equal(enrollment.ItemId, access.Enrollment.ItemId);
        Assert.Equal(CourseItemAccessReason.FreePreview, stranger.Reason);
    }

    [Fact]
    public async Task GetItemAccessAsync_IgnoresContentThatIsNotACourseItem()
    {
        // Arrange
        var (harness, _, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        var page = new ContentItem { ContentItemId = "page", ContentType = "Page" };

        // Act
        var access = await harness.RunAsync(services => services.GetRequiredService<ICourseAccessService>().GetItemAccessAsync(CalendarTestHarness.User("someone"), page), commit: false);

        // Assert
        Assert.Equal(CourseItemAccessReason.NotACourseItem, access.Reason);
    }

    [Fact]
    public async Task ContentAccessHandler_FailsViewContentForAPersonWhoIsNotEnrolled_EvenWithTheGeneralPermission()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        var lesson = courses.AddItem("course1", "lesson");
        await EnrollAsync(harness, "learner", "course1");

        // Act
        Task<AuthorizationHandlerContext> Check(ClaimsPrincipal user, ContentItem item)
            => harness.RunAsync(async services =>
            {
                var handler = ActivatorUtilities.CreateInstance<LearningContentAccessHandler>(services);
                var context = new AuthorizationHandlerContext([new PermissionRequirement(CommonPermissions.ViewContent)], user, item);

                await handler.HandleAsync(context);

                return context;
            }, commit: false);

        var stranger = await Check(CalendarTestHarness.User("stranger", CommonPermissions.ViewContent.Name), lesson);
        var learner = await Check(CalendarTestHarness.User("learner"), lesson);
        var other = await Check(CalendarTestHarness.User("stranger"), new ContentItem { ContentItemId = "page", ContentType = "Page" });

        // Assert
        Assert.True(stranger.HasFailed);
        Assert.True(learner.HasSucceeded);
        Assert.False(other.HasFailed);
        Assert.False(other.HasSucceeded);
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string courseId, DateTime? accessEndsUtc = null, bool requiresApproval = false)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
            AccessEndsUtc = accessEndsUtc,
            RequiresApproval = requiresApproval,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }
}
