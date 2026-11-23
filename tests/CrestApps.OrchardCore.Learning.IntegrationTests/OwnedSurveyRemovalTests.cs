using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Review V-M4 and V-M2 on the SQLite test site: removing a course item removes its survey and the records that hang off
/// it after the request commits, without the purge waiting on the request's own writes; and the course items' surveys
/// are listed from the content index.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class OwnedSurveyRemovalTests
{
    private readonly LearningSiteFixture _fixture;

    public OwnedSurveyRemovalTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task VM4_RemovingAQuiz_DeletesItsSurveyAndItsAccommodations_OnSqlite()
    {
        // Arrange: a quiz whose survey has an accommodation, so the purge has a record to delete.
        var (quizId, surveyId) = await CreateQuizAsync();

        await _fixture.InTenantAsync(async services =>
        {
            var granted = await services.GetRequiredService<ISurveyOverrideService>().GrantAsync(new SurveyOverride
            {
                SurveyId = surveyId,
                UserId = _fixture.EnrolledUserId,
                ExtraMinutes = 10,
                Reason = "Accommodation",
            });

            Assert.Equal(SurveyErrorCode.None, granted);
        });

        // Act: remove the quiz in a request of its own, timed.
        var started = DateTime.UtcNow;

        await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var quiz = await contentManager.GetAsync(quizId, VersionOptions.Latest);

            await contentManager.RemoveAsync(quiz);
        });

        var elapsed = DateTime.UtcNow - started;

        // Assert: the survey and its accommodation are gone, well within SQLite's busy timeout.
        var (survey, overrides) = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ISurveyManager>().FindByIdAsync(surveyId),
             await services.GetRequiredService<ISurveyOverrideService>().ListAsync(surveyId)));

        Assert.Null(survey);
        Assert.Empty(overrides);
        Assert.True(elapsed < TimeSpan.FromSeconds(20), $"Removing the quiz took {elapsed}.");
    }

    [Fact]
    public async Task VM2_TheCourseItemOwners_AreListedFromTheContentIndex()
    {
        // Arrange
        var (quizId, _) = await CreateQuizAsync();

        // Act
        var owners = await _fixture.InTenantAsync(services => services.GetRequiredService<ILearningSurveyService>().ListCourseItemOwnersAsync());

        // Assert
        Assert.Contains(LearningSurveyContexts.Owner(quizId), owners);
    }

    private Task<(string QuizId, string SurveyId)> CreateQuizAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var suffix = Guid.NewGuid().ToString("n")[..6];
            var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

            course.DisplayText = "Removal " + suffix;
            course.Alter<TitlePart>(part => part.Title = course.DisplayText);
            await contentManager.CreateAsync(course, VersionOptions.Draft);

            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Removal quiz " + suffix;
            quiz.Alter<TitlePart>(part => part.Title = quiz.DisplayText);
            quiz.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest);
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(quiz);

            Assert.False(string.IsNullOrEmpty(surveyId));

            return (quiz.ContentItemId, surveyId);
        });
}
