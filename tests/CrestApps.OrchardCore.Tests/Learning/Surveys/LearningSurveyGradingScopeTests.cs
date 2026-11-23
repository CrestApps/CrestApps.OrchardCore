using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// The surveys course staff may grade are the surveys of the latest version of every item their courses contain, a draft
/// quiz included, looked up at once rather than once per item. Only the courses where the user grades count.
/// </summary>
public sealed class LearningSurveyGradingScopeTests
{
    [Fact]
    public async Task ADraftQuizsSurvey_IsGradable_ForTheCoursesStaff()
    {
        // Arrange: a published quiz and a quiz whose latest version is an unpublished draft, in a course the user grades.
        var courses = new InMemoryCourses();
        courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Grader }]);
        courses.AddItem("course1", "quiz1");
        var draft = courses.AddItem("course1", "quiz2", sectionId: null);
        draft.Published = false;
        var surveyManager = SurveyManager();

        // Act
        var ids = await Scope(courses, surveyManager).GetGradableSurveyIdsAsync(User("grader"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["survey1", "survey2"], ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ManyItems_AreLookedUpInOneOwnersLookup()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
        courses.AddCourse("course2", part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Assistant }]);

        for (var i = 0; i < 50; i++)
        {
            courses.AddItem(i % 2 == 0 ? "course1" : "course2", "quiz-" + i);
        }

        var surveyManager = SurveyManager();

        // Act
        var ids = await Scope(courses, surveyManager).GetGradableSurveyIdsAsync(User("teacher"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(50, ids.Count);
        surveyManager.Verify(manager => manager.ListByOwnersAsync(It.Is<IReadOnlyCollection<RecordRef>>(owners => owners.Count == 50), It.IsAny<CancellationToken>()), Times.Once());
        surveyManager.Verify(manager => manager.ListByOwnerAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task OnlyTheCoursesWhereTheUserGrades_Count()
    {
        // Arrange: the user observes course2 and is not on course3's staff.
        var courses = new InMemoryCourses();
        courses.AddCourse("course1", part => part.Staff = [new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Grader }]);
        courses.AddCourse("course2", part => part.Staff = [new CourseStaffMember { UserId = "grader", Role = LearningConstants.CourseStaffRoles.Observer }]);
        courses.AddCourse("course3");
        courses.AddItem("course1", "quiz1");
        courses.AddItem("course2", "quiz2");
        courses.AddItem("course3", "quiz3");

        // Act
        var ids = await Scope(courses, SurveyManager()).GetGradableSurveyIdsAsync(User("grader"), TestContext.Current.CancellationToken);
        var withoutPermission = await Scope(courses, SurveyManager(), grant: false).GetGradableSurveyIdsAsync(User("grader"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["survey1"], ids);
        Assert.Empty(withoutPermission);
    }

    private static LearningSurveyGradingScope Scope(InMemoryCourses courses, Mock<ISurveyManager> surveyManager, bool grant = true)
        => new(courses, surveyManager.Object, Authorization(grant ? [LearningPermissions.ManageOwnCourses.Name] : []));

    private static Mock<ISurveyManager> SurveyManager()
    {
        // Each quiz owns one survey: quiz1 owns survey1, quiz-7 owns survey-7.
        var surveyManager = new Mock<ISurveyManager>();
        surveyManager
            .Setup(manager => manager.ListByOwnersAsync(It.IsAny<IReadOnlyCollection<RecordRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<RecordRef> owners, CancellationToken _) => owners
                .Where(owner => owner.Id.StartsWith("quiz", StringComparison.Ordinal))
                .Select(owner => new Survey { ItemId = string.Concat("survey", owner.Id.AsSpan("quiz".Length)), Kind = SurveyKinds.Quiz, Owner = owner })
                .ToArray());

        return surveyManager;
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }

    private static ClaimsPrincipal User(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
}
