using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// R4-L5, on a real database: the course grading list finds a course's quizzes from the latest version of every item
/// the course contains (an unpublished course and a draft quiz included, an item moved to another course left out),
/// and looks up the surveys those items own in one query.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class CourseGradingQueriesTests
{
    private readonly LearningSiteFixture _fixture;

    public CourseGradingQueriesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ContainedItems_IncludeDraftsOfAnUnpublishedCourse_AndTheirSurveysAreFoundInOneLookup()
    {
        var result = await _fixture.InTenantAsync(async services =>
        {
            // Arrange: a course that was never published, with a published page and a quiz saved only as a draft, and an
            // item whose latest version moved to another course.
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await NewCourseAsync(contentManager, "Draft course " + suffix);
            var other = await NewCourseAsync(contentManager, "Other course " + suffix);
            var page = await NewItemAsync(contentManager, course, LearningConstants.ContentTypes.PageLesson, "Page " + suffix, VersionOptions.Published);
            var quiz = await NewItemAsync(contentManager, course, AssessmentsConstants.ContentTypes.Quiz, "Quiz " + suffix, VersionOptions.Draft);
            var moved = await NewItemAsync(contentManager, course, LearningConstants.ContentTypes.PageLesson, "Moved " + suffix, VersionOptions.Published);

            moved.Alter<ContainedPart>(part => part.ListContentItemId = other.ContentItemId);
            await contentManager.UpdateAsync(moved);
            await contentManager.PublishAsync(moved);

            // Act
            var items = await services.GetRequiredService<ICourseStructureService>().GetContainedItemsAsync([course.ContentItemId]);
            var surveys = await services.GetRequiredService<ISurveyManager>().ListByOwnersAsync(
                items.Select(item => LearningSurveyContexts.Owner(item.ContentItemId)).ToArray());

            return (
                ItemIds: items.Select(item => item.ContentItemId).Order(StringComparer.Ordinal).ToArray(),
                Expected: new[] { page.ContentItemId, quiz.ContentItemId }.Order(StringComparer.Ordinal).ToArray(),
                SurveyIds: surveys.Select(survey => survey.ItemId).ToArray(),
                QuizSurveyId: LearningSurveyTestSupport.SurveyIdOf(quiz));
        });

        // Assert
        Assert.Equal(result.Expected, result.ItemIds);
        Assert.False(string.IsNullOrEmpty(result.QuizSurveyId));
        Assert.Contains(result.QuizSurveyId, result.SurveyIds);
    }

    [Fact]
    public async Task GradingScope_LetsTheCoursesStaffGradeADraftQuiz()
    {
        var result = await _fixture.InTenantAsync(async services =>
        {
            // Arrange: a published course the instructor teaches, with a quiz saved only as a draft.
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await NewCourseAsync(contentManager, "Taught course " + suffix);

            course.Alter<CoursePart>(part => part.Staff = [new CourseStaffMember { UserId = _fixture.InstructorId, Role = LearningConstants.CourseStaffRoles.Instructor }]);
            await contentManager.UpdateAsync(course);
            await contentManager.PublishAsync(course);

            var quiz = await NewItemAsync(contentManager, course, AssessmentsConstants.ContentTypes.Quiz, "Draft quiz " + suffix, VersionOptions.Draft);

            var user = await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(_fixture.InstructorId);
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

            // Act
            var scope = services.GetServices<ISurveyGradingScope>().OfType<LearningSurveyGradingScope>().Single();
            var gradable = await scope.GetGradableSurveyIdsAsync(principal);

            return (Gradable: gradable, QuizSurveyId: LearningSurveyTestSupport.SurveyIdOf(quiz));
        });

        // Assert
        Assert.False(string.IsNullOrEmpty(result.QuizSurveyId));
        Assert.Contains(result.QuizSurveyId, result.Gradable);
    }

    private static async Task<ContentItem> NewCourseAsync(IContentManager contentManager, string title)
    {
        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

        course.DisplayText = title;
        course.Alter<TitlePart>(part => part.Title = title);
        course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        return course;
    }

    private static async Task<ContentItem> NewItemAsync(IContentManager contentManager, ContentItem course, string type, string title, VersionOptions options)
    {
        var item = await contentManager.NewAsync(type);

        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<HtmlBodyPart>(part => part.Html = $"<p>{title}</p>");
        item.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Viewed);

        if (type == AssessmentsConstants.ContentTypes.Quiz)
        {
            item.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest);
            item.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
        }

        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        await contentManager.CreateAsync(item, options);

        return item;
    }
}
