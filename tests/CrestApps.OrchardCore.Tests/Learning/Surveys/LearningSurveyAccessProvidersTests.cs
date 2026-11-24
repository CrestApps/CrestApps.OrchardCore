using System.Security.Claims;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
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

        var provider = new LearningSurveyAccessProvider(learningSurveys.Object, structure.Object, Authorization(LearningPermissions.ManageOwnCourses), [], Mock.Of<IServiceProvider>());

        // Act
        var granted = await provider.GrantsAsync(User(), OwnedSurvey(), operation, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, granted);
    }

    [Theory]
    // A quiz: voiding or deleting an attempt changes the grade, so an instructor needs Override grades.
    [InlineData(SurveyKinds.Quiz, false, false, false)]
    [InlineData(SurveyKinds.Quiz, false, true, true)]
    // A course survey that gives no score stays with the instructors and assistants.
    [InlineData(SurveyKinds.Survey, false, false, true)]
    // A course survey that gives a score is graded like a quiz.
    [InlineData(SurveyKinds.Survey, true, false, false)]
    public async Task R6M7_AnInstructor_VoidsOrDeletesTheResponsesOfAGradedSurvey_OnlyWithOverrideGrades(string kind, bool scored, bool canOverride, bool expected)
    {
        // Arrange: instructor1 teaches the course of the item that owns the survey.
        var item = new ContentItem { ContentItemId = "quiz1" };
        var learningSurveys = Owned(true);
        learningSurveys.Setup(service => service.FindOwnerItemAsync(It.IsAny<Survey>())).ReturnsAsync(item);

        var structure = new Mock<ICourseStructureService>();
        structure.Setup(service => service.GetCourseId(item)).Returns("course1");
        structure.Setup(service => service.IsCourseStaffAsync(UserId, "course1", It.IsAny<string[]>())).ReturnsAsync(true);

        var authorization = canOverride
            ? Authorization(LearningPermissions.ManageOwnCourses, AssessmentPermissions.OverrideGrades)
            : Authorization(LearningPermissions.ManageOwnCourses);
        var provider = new LearningSurveyAccessProvider(learningSurveys.Object, structure.Object, authorization, [new AssessmentsScoreChangeCheck(learningSurveys.Object, authorization)], Mock.Of<IServiceProvider>());
        var survey = OwnedSurvey();
        survey.Kind = kind;
        survey.Draft.Scoring.Enabled = scored;

        // Act
        var delete = await provider.GrantsAsync(User(), survey, SurveyOperation.DeleteResponses, TestContext.Current.CancellationToken);
        var manage = await provider.GrantsAsync(User(), survey, SurveyOperation.Manage, TestContext.Current.CancellationToken);
        var listed = await provider.ListGrantsAsync(User(), SurveyOperation.DeleteResponses, TestContext.Current.CancellationToken);

        // Assert: the survey itself stays theirs to manage; the delete scope asks about each survey.
        Assert.Equal(expected, delete);
        Assert.True(manage);
        Assert.Null(listed);
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

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public async Task ChangingAGivenScore_OfASurveyACourseItemOwns_NeedsOverrideGrades(bool owned, bool canOverride, bool expected)
    {
        // Arrange: a grader who may grade every course; Override grades only when the case says so.
        var check = new AssessmentsScoreChangeCheck(
            Owned(owned).Object,
            canOverride ? Authorization(AssessmentPermissions.GradeAssessments, AssessmentPermissions.OverrideGrades) : Authorization(AssessmentPermissions.GradeAssessments));
        var survey = owned ? OwnedSurvey() : new Survey { ItemId = "hr" };

        // Act
        var allowed = await check.AllowsAsync(User(), survey, new SurveyResponse { ItemId = "r1", Grading = GradingStatus.Graded }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, allowed);
    }

    [Theory]
    [InlineData(SurveyOperation.Manage, true)]
    [InlineData(SurveyOperation.ViewResponses, true)]
    [InlineData(SurveyOperation.Grade, false)]
    public async Task VM2_TheManagerProvider_ListsEveryCourseItemAsAnOwner_WithoutAskingAboutEachSurvey(SurveyOperation operation, bool expected)
    {
        // Arrange
        var learningSurveys = Owned(true);
        learningSurveys.Setup(service => service.ListCourseItemOwnersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([LearningSurveyContexts.Owner("quiz1"), LearningSurveyContexts.Owner("quiz2")]);
        var provider = new LearningManagerSurveyAccessProvider(learningSurveys.Object, Authorization(LearningPermissions.ManageCourses, LearningPermissions.ViewLearnerRecords));

        // Act
        var grants = await provider.ListGrantsAsync(User(), operation, TestContext.Current.CancellationToken);

        // Assert: a list, never null (null makes the scope ask about every survey), and no survey asked about.
        Assert.NotNull(grants);
        Assert.Equal(expected ? 2 : 0, grants.Owners.Count);
        learningSurveys.Verify(service => service.IsOwnedByCourseItemAsync(It.IsAny<Survey>()), Times.Never);
    }

    [Theory]
    [InlineData(SurveyOperation.Grade, true)]
    [InlineData(SurveyOperation.DeleteResponses, false)]
    [InlineData(SurveyOperation.Manage, false)]
    public async Task VM2_TheAssessmentsProvider_ListsEveryCourseItemAsAnOwner_WithoutAskingAboutEachSurvey(SurveyOperation operation, bool expected)
    {
        // Arrange: the grader holds Grade assessments but not Override grades.
        var learningSurveys = Owned(true);
        learningSurveys.Setup(service => service.ListCourseItemOwnersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([LearningSurveyContexts.Owner("quiz1")]);
        var provider = new AssessmentsSurveyAccessProvider(learningSurveys.Object, Authorization(AssessmentPermissions.GradeAssessments));

        // Act
        var grants = await provider.ListGrantsAsync(User(), operation, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(grants);
        Assert.Equal(expected ? 1 : 0, grants.Owners.Count);
        learningSurveys.Verify(service => service.IsOwnedByCourseItemAsync(It.IsAny<Survey>()), Times.Never);
    }

    [Fact]
    public async Task VM2_TheLearningProviders_ListNothingForAnonymousUsers()
    {
        // Arrange
        var learningSurveys = Owned(true);
        var everything = Authorization(LearningPermissions.ManageCourses, AssessmentPermissions.GradeAssessments);
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var manager = await new LearningManagerSurveyAccessProvider(learningSurveys.Object, everything).ListGrantsAsync(anonymous, SurveyOperation.Manage, TestContext.Current.CancellationToken);
        var assessments = await new AssessmentsSurveyAccessProvider(learningSurveys.Object, everything).ListGrantsAsync(anonymous, SurveyOperation.Grade, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(manager.Owners);
        Assert.Empty(assessments.Owners);
        learningSurveys.Verify(service => service.ListCourseItemOwnersAsync(It.IsAny<CancellationToken>()), Times.Never);
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
