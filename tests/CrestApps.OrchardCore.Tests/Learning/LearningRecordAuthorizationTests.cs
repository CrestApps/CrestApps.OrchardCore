using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningRecordAuthorizationTests
{
    [Theory]
    [InlineData("learner", "ViewLearnerRecords", true)]
    [InlineData("learner", "ManageEnrollments", false)]
    [InlineData("other-learner", "ViewLearnerRecords", false)]
    [InlineData("teacher", "ViewEnrollments", true)]
    [InlineData("teacher", "ViewLearnerRecords", true)]
    [InlineData("teacher", "ManageEnrollments", true)]
    [InlineData("grader", "ViewLearnerRecords", true)]
    [InlineData("grader", "ManageEnrollments", false)]
    [InlineData("observer", "ViewLearnerRecords", true)]
    [InlineData("observer", "ManageEnrollments", false)]
    [InlineData("other-teacher", "ViewLearnerRecords", false)]
    public async Task RecordHandler_GrantsTheLearnerAndTheCourseStaffOnly(string userId, string permissionName, bool granted)
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1", part => part.Staff =
        [
            new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
            new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Grader },
            new CourseStaffMember { UserId = "observer", Role = LearningConstants.CourseStaffRoles.Observer },
        ]);
        courses.AddCourse("course2", part => part.Staff = [new CourseStaffMember { UserId = "other-teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
        var enrollment = new Enrollment { ItemId = "e1", UserId = "learner", CourseId = "course1" };
        var permission = new[] { LearningPermissions.ViewLearnerRecords, LearningPermissions.ManageEnrollments, LearningPermissions.ViewEnrollments }.Single(candidate => candidate.Name == permissionName);

        // Act
        var context = await harness.RunAsync(async services =>
        {
            var handler = ActivatorUtilities.CreateInstance<LearningRecordAuthorizationHandler>(services);
            var authorization = new AuthorizationHandlerContext([new PermissionRequirement(permission)], CalendarTestHarness.User(userId), enrollment);

            await handler.HandleAsync(authorization);

            return authorization;
        }, commit: false);

        // Assert
        Assert.Equal(granted, context.HasSucceeded);
        Assert.False(context.HasFailed);
    }

    [Fact]
    public async Task RecordHandler_IgnoresAnonymousPeopleAndOtherResources()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");

        // Act
        var results = await harness.RunAsync(async services =>
        {
            var handler = ActivatorUtilities.CreateInstance<LearningRecordAuthorizationHandler>(services);
            var anonymous = new AuthorizationHandlerContext([new PermissionRequirement(LearningPermissions.ViewLearnerRecords)], new ClaimsPrincipal(new ClaimsIdentity()), new Enrollment { UserId = null, CourseId = "course1" });
            var other = new AuthorizationHandlerContext([new PermissionRequirement(LearningPermissions.ViewLearnerRecords)], CalendarTestHarness.User("learner"), "not an enrollment");

            await handler.HandleAsync(anonymous);
            await handler.HandleAsync(other);

            return (anonymous.HasSucceeded, other.HasSucceeded);
        }, commit: false);

        // Assert
        Assert.False(results.Item1);
        Assert.False(results.Item2);
    }
}
