using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class CourseStaffContentAuthorizationTests
{
    [Theory]
    [InlineData("teacher", true, "EditContent", "course", true)]
    [InlineData("teacher", true, "PublishContent", "lesson", true)]
    [InlineData("teacher", true, "DeleteContent", "lesson", false)]
    [InlineData("teacher", false, "EditContent", "course", false)]
    [InlineData("grader", true, "EditContent", "course", false)]
    [InlineData("other", true, "EditContent", "lesson", false)]
    public async Task Handler_LetsInstructorsWithManageOwnCoursesEditTheirCourses(string userId, bool hasManageOwnCourses, string permissionName, string itemName, bool granted)
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        var course = courses.AddCourse("course1", part => part.Staff =
        [
            new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
            new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Grader },
        ]);
        var lesson = courses.AddItem("course1", "lesson1");
        var item = itemName == "course" ? course : lesson;
        var permission = new[] { CommonPermissions.EditContent, CommonPermissions.PublishContent, CommonPermissions.DeleteContent }.Single(candidate => candidate.Name == permissionName);
        var user = hasManageOwnCourses ? CalendarTestHarness.User(userId, LearningPermissions.ManageOwnCourses.Name) : CalendarTestHarness.User(userId);

        // Act
        var context = await harness.RunAsync(async services =>
        {
            var handler = ActivatorUtilities.CreateInstance<CourseStaffContentAuthorizationHandler>(services);
            var authorization = new AuthorizationHandlerContext([new PermissionRequirement(permission)], user, item);

            await handler.HandleAsync(authorization);

            return authorization;
        }, commit: false);

        // Assert
        Assert.Equal(granted, context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task Handler_IgnoresContentOutsideCourses()
    {
        // Arrange
        var (harness, _, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;

        // Act
        var context = await harness.RunAsync(async services =>
        {
            var handler = ActivatorUtilities.CreateInstance<CourseStaffContentAuthorizationHandler>(services);
            var authorization = new AuthorizationHandlerContext([new PermissionRequirement(CommonPermissions.EditContent)], CalendarTestHarness.User("teacher", LearningPermissions.ManageOwnCourses.Name), new ContentItem { ContentItemId = "page", ContentType = "Page" });

            await handler.HandleAsync(authorization);

            return authorization;
        }, commit: false);

        // Assert
        Assert.False(context.HasSucceeded);
    }
}
