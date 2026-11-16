using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
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
        var attempt = new AssessmentAttempt { ItemId = "a1", UserId = "learner", CourseId = "course1" };
        var user = staffPermission ? CalendarTestHarness.User(userId, LearningPermissions.ManageOwnCourses.Name) : CalendarTestHarness.User(userId);

        // Act
        var canGrade = await h.RunAsync(services => ActivatorUtilities.CreateInstance<GradingScope>(services).CanGradeAsync(user, attempt));

        // Assert
        Assert.Equal(granted, canGrade);
    }

    [Fact]
    public async Task NobodyGradesTheirOwnAttempt_NotEvenWithTheSiteWidePermission()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
        var own = new AssessmentAttempt { ItemId = "a1", UserId = "teacher", CourseId = "course1" };
        var grader = CalendarTestHarness.User("teacher", AssessmentPermissions.GradeAssessments.Name, LearningPermissions.ManageOwnCourses.Name);

        // Act
        var canGrade = await h.RunAsync(services => ActivatorUtilities.CreateInstance<GradingScope>(services).CanGradeAsync(grader, own));

        // Assert
        Assert.False(canGrade);
    }

    [Fact]
    public async Task TheAttemptService_RefusesGradingRegradingOrVoidingOnesOwnAttempt()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        // Act
        var grade = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore>(), "Mine", "learner"));
        var regrade = await h.RunAsync(service => service.RegradeAsync(attempt.ItemId, "learner", "Mine"));
        var voided = await h.RunAsync(service => service.VoidAsync(attempt.ItemId, "learner", "Mine"));

        // Assert
        Assert.Equal(AttemptErrors.OwnAttempt, grade.ErrorCode);
        Assert.Equal(AttemptErrors.OwnAttempt, regrade.ErrorCode);
        Assert.Equal(AttemptErrors.OwnAttempt, voided.ErrorCode);
    }
}
