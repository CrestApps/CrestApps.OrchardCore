using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

public sealed class GradingScopeTests
{
    [Theory]
    [InlineData("grader", true, true)]
    [InlineData("grader", false, false)]
    [InlineData("teacher", true, true)]
    [InlineData("teacher", false, false)]
    [InlineData("observer", true, false)]
    [InlineData("other-grader", true, false)]
    public async Task CourseStaff_GradeTheirCoursesAttempts_OnlyWithALearningPermission(string userId, bool staffPermission, bool granted)
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1", part => part.Staff =
        [
            new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Grader },
            new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor },
            new CourseStaffMember { UserId = "observer", Role = LearningConstants.CourseStaffRoles.Observer },
        ]);
        h.Courses.AddCourse("course2", part => part.Staff = [new CourseStaffMember { UserId = "other-grader", Role = LearningConstants.CourseStaffRoles.Grader }]);
        var user = staffPermission ? CalendarTestHarness.User(userId, LearningPermissions.ManageOwnCourses.Name) : CalendarTestHarness.User(userId);

        // Act
        var canGrade = await h.RunAsync(services => ActivatorUtilities.CreateInstance<GradingScope>(services).CanGradeAsync(user, "learner", "course1"));

        // Assert
        Assert.Equal(granted, canGrade);
    }

    [Fact]
    public async Task NobodyGradesTheirOwnAttempt_NotEvenWithTheSiteWidePermission()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
        var grader = CalendarTestHarness.User("teacher", AssessmentPermissions.GradeAssessments.Name, LearningPermissions.ManageOwnCourses.Name);

        // Act
        var canGrade = await h.RunAsync(services => ActivatorUtilities.CreateInstance<GradingScope>(services).CanGradeAsync(grader, "teacher", "course1"));

        // Assert
        Assert.False(canGrade);
    }
}
