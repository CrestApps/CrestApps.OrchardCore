using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Production review P4-L1 and P4-L2: graders of an unpublished quiz get every grading tool, as the grading scope gives
/// them the grading itself; and the site-wide Learning roles reach the surveys course items own through Learning
/// permissions, never other surveys, so the roles recipe needs no site-wide Surveys permission.
/// </summary>
public sealed class LearningSurveyAccessProvidersTests
{
    private const string UserId = "grader1";

    [Theory]
    [InlineData(SurveyOperation.Grade, true)]
    [InlineData(SurveyOperation.ViewResponses, true)]
    [InlineData(SurveyOperation.Manage, false)]
    public async Task P4L1_AGraderOfAnUnpublishedQuiz_GetsGradingAndResponses(SurveyOperation operation, bool expected)
    {
        // Arrange: the quiz has no published version, only a latest one in the grader's course.
        var item = new ContentItem { ContentItemId = "quiz1" };
        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys.Setup(service => service.FindOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync((ContentItem)null);
        learningSurveys.Setup(service => service.FindLatestOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync(item);

        var structure = new Mock<ICourseStructureService>();
        structure.Setup(service => service.GetCourseId(item)).Returns("course1");
        structure.Setup(service => service.IsCourseStaffAsync(UserId, "course1", It.IsAny<string[]>())).ReturnsAsync(true);

        var provider = new LearningSurveyAccessProvider(learningSurveys.Object, structure.Object, Authorization(LearningPermissions.ManageOwnCourses));

        // Act
        var granted = await provider.GrantsAsync(User(), OwnedSurvey(), operation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, granted);
    }

    [Theory]
    [InlineData("ManageCourses", SurveyOperation.Manage, true)]
    [InlineData("ManageCourses", SurveyOperation.Publish, true)]
    [InlineData("ManageCourses", SurveyOperation.Grade, false)]
    [InlineData("ViewLearnerRecords", SurveyOperation.ViewResponses, true)]
    [InlineData("ManageEnrollments", SurveyOperation.ManageOverrides, true)]
    [InlineData("ManageEnrollments", SurveyOperation.ExportResponses, false)]
    public async Task P4L2_SiteWideLearningPermissions_ReachTheSurveysCourseItemsOwn(string permission, SurveyOperation operation, bool expected)
    {
        // Arrange
        var provider = new LearningManagerSurveyAccessProvider(Owned(true).Object, Authorization(PermissionNamed(permission)));

        // Act
        var granted = await provider.GrantsAsync(User(), OwnedSurvey(), operation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, granted);
    }

    [Theory]
    [InlineData(SurveyOperation.Grade)]
    [InlineData(SurveyOperation.ViewResponses)]
    public async Task P4L2_GradeAssessments_GradesTheSurveysCourseItemsOwn(SurveyOperation operation)
    {
        // Arrange
        var provider = new AssessmentsSurveyAccessProvider(Owned(true).Object, Authorization(AssessmentPermissions.GradeAssessments));

        // Act + Assert
        Assert.True(await provider.GrantsAsync(User(), OwnedSurvey(), operation, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task P4L2_TheLearningProviders_GiveNothingOnASurveyNoCourseItemOwns()
    {
        // Arrange: an HR survey, owned by no course item.
        var survey = new Survey { ItemId = "hr" };
        var everything = Authorization(LearningPermissions.ManageCourses, LearningPermissions.ViewLearnerRecords, LearningPermissions.ManageEnrollments, AssessmentPermissions.GradeAssessments, AssessmentPermissions.OverrideGrades);
        var manager = new LearningManagerSurveyAccessProvider(Owned(false).Object, everything);
        var assessments = new AssessmentsSurveyAccessProvider(Owned(false).Object, everything);

        // Act + Assert
        foreach (var operation in Enum.GetValues<SurveyOperation>())
        {
            Assert.False(await manager.GrantsAsync(User(), survey, operation, TestContext.Current.CancellationToken));
            Assert.False(await assessments.GrantsAsync(User(), survey, operation, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void P4L2_TheLearningRolesRecipe_GrantsNoSiteWideSurveysPermission()
    {
        // Arrange
        var path = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Learning", "Recipes", "learning-roles.recipe.json");
        var text = File.ReadAllText(path);

        // Act + Assert
        foreach (var permission in new[] { "\"ManageSurveys\"", "\"PublishSurveys\"", "\"ViewSurveyResponses\"", "\"GradeSurveyResponses\"", "\"ManageSurveyOverrides\"", "\"DeleteSurveyResponses\"", "\"ExportSurveyResponses\"" })
        {
            Assert.DoesNotContain(permission, text);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Learning")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static Permission PermissionNamed(string name)
        => name switch
        {
            "ManageCourses" => LearningPermissions.ManageCourses,
            "ViewLearnerRecords" => LearningPermissions.ViewLearnerRecords,
            "ManageEnrollments" => LearningPermissions.ManageEnrollments,
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static Mock<ILearningSurveyService> Owned(bool owned)
    {
        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys.Setup(service => service.IsOwnedByCourseItemAsync(It.IsAny<Survey>())).ReturnsAsync(owned);

        return learningSurveys;
    }

    private static Survey OwnedSurvey()
        => new() { ItemId = "survey1", Owner = LearningSurveyContexts.Owner("quiz1") };

    private static ClaimsPrincipal User()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"));

    // Grants exactly the given permissions, by name.
    private static IAuthorizationService Authorization(params Permission[] granted)
    {
        var names = granted.Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => names.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorization.Object;
    }
}
