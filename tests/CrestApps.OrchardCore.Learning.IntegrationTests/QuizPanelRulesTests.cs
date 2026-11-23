using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The quiz and course survey rules on a running site: a quiz's pass is the survey's own decision (score bands, then the
/// pass mark), the quiz panel shows a score only as the review settings allow and offers another attempt as the attempt
/// limit (with accommodations) decides, and an anonymous course survey's completion is recorded by the day only.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizPanelRulesTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizPanelRulesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AQuizWithAFailingBandAndNoPassMark_IsNotPassedOnTheCourse()
    {
        // Arrange
        var setup = await CreateQuizCourseAsync(definition =>
        {
            definition.Scoring.PassMarkPercent = null;
            definition.ScoreBands =
            [
                new ScoreBand { Id = "fail", MinPercent = 0, Label = "Fail", Passing = false },
                new ScoreBand { Id = "pass", MinPercent = 70, Label = "Pass", Passing = true },
            ];
        }, survey => { });
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{setup.CourseId}/play/{setup.QuizId}", TestContext.Current.CancellationToken);

        // Act
        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/quizzes/{setup.QuizId}/start", player, new Dictionary<string, string>
        {
            ["q.planet"] = "venus",
        });
        var (progress, responses) = await ReadAsync(setup);

        // Assert
        var response = Assert.Single(responses);
        Assert.Equal(0m, response.Percent);
        Assert.False(response.Passed);
        Assert.False(progress?.Passed);
        Assert.NotEqual(CourseItemState.Completed, progress?.State);
    }

    [Fact]
    public async Task TheQuizPanel_HidesAHiddenScore_AndOffersTheAttemptAnAccommodationGrants()
    {
        // Arrange: one attempt allowed, scores never shown to respondents.
        var setup = await CreateQuizCourseAsync(
            definition => definition.Scoring.ShowScoreToRespondent = false,
            survey =>
            {
                survey.Access ??= new SurveyAccessSettings();
                survey.Access.Limits = new RespondentLimits { Total = 1, Counts = LimitCount.Started };
            });
        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{setup.CourseId}/play/{setup.QuizId}";
        var player = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/quizzes/{setup.QuizId}/start", player, new Dictionary<string, string>
        {
            ["q.planet"] = "venus",
        });

        // Act
        var afterAttempt = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        await _fixture.InTenantAsync(async services =>
        {
            var granted = await services.GetRequiredService<ISurveyOverrideService>().GrantAsync(new SurveyOverride
            {
                SurveyId = setup.SurveyId,
                UserId = _fixture.EnrolledUserId,
                ExtraAttempts = 1,
                Reason = "Test accommodation",
                Actor = "admin",
            });

            Assert.Equal(SurveyErrorCode.None, granted);
        });
        var afterAccommodation = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("data-learning-quiz-score-hidden", afterAttempt);
        Assert.DoesNotContain("text-bg-danger ms-1", afterAttempt);
        Assert.DoesNotContain("Attempt again", afterAttempt);
        Assert.Contains("0 attempts left", afterAttempt);
        Assert.Contains("Attempt again", afterAccommodation);
        Assert.Contains("1 attempts left", afterAccommodation);
    }

    [Fact]
    public async Task AnAnonymousCourseSurvey_IsRecordedByTheDayOnly()
    {
        // Arrange
        var setup = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Anonymous feedback");
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Course feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(item);

            await LearningSurveyTestSupport.PublishAsync(
                services,
                surveyId,
                LearningSurveyTestSupport.Definition("Course feedback", LearningSurveyTestSupport.SingleChoice("again", "Would you take it again?", ("yes", "Yes"), ("no", "No"))),
                survey =>
                {
                    survey.Access ??= new SurveyAccessSettings();
                    survey.Access.Anonymity = AnonymityMode.Anonymous;
                });

            // A second item keeps the course from completing, so the item is recorded on an active enrollment.
            var lesson = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Next step", CompletionModes.Submitted);

            lesson.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            var enrollmentId = await PublishAndEnrollAsync(services, contentManager, course, [item.ContentItemId, lesson.ContentItemId]);

            return (CourseId: course.ContentItemId, ItemId: item.ContentItemId, EnrollmentId: enrollmentId);
        });
        using var learner = await _fixture.CreateClientAsync("learner1");
        var player = await learner.GetStringAsync($"learning/courses/{setup.CourseId}/play/{setup.ItemId}", TestContext.Current.CancellationToken);
        var lastAccessBefore = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.EnrollmentId))?.Find(setup.ItemId)?.LastAccessUtc);

        // Act
        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{setup.ItemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
        });
        var progress = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<ILearnerProgressService>().GetAsync(setup.EnrollmentId))?.Find(setup.ItemId));
        var siteZone = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<global::OrchardCore.Settings.ISiteService>().GetSiteSettingsAsync()).TimeZoneId);

        // Assert
        Assert.Equal(CourseItemState.Completed, progress?.State);
        Assert.NotNull(progress.CompletedUtc);
        Assert.Equal(LearningSurveyResultObserver.StartOfDayUtc(progress.CompletedUtc.Value, siteZone), progress.CompletedUtc);
        Assert.Equal(lastAccessBefore, progress.LastAccessUtc);
    }

    [Fact]
    public async Task TheCourseSurveyPanel_OffersToChangeAnswers_OnlyWhenThePublishedVersionAllowsIt()
    {
        // Arrange: the published version doesn't allow changing a finished response; the unpublished draft does.
        var setup = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Draft settings");
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Course feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(item);

            await LearningSurveyTestSupport.PublishAsync(services, surveyId, Feedback());

            var manager = services.GetRequiredService<ISurveyManager>();
            var survey = await manager.FindByIdAsync(surveyId);
            var draft = Feedback();

            draft.Navigation.AllowEditAfterSubmit = true;
            Assert.Equal(SurveyErrorCode.None, await manager.SaveDraftAsync(survey, draft));

            var lesson = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.Choice, "Next step", CompletionModes.Submitted);

            lesson.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(lesson, VersionOptions.Published);

            await PublishAndEnrollAsync(services, contentManager, course, [item.ContentItemId, lesson.ContentItemId]);

            return (CourseId: course.ContentItemId, ItemId: item.ContentItemId);
        });
        using var learner = await _fixture.CreateClientAsync("learner1");
        var path = $"learning/courses/{setup.CourseId}/play/{setup.ItemId}";
        var player = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Act
        await LearningSurveyTestSupport.AnswerOverHttpAsync(learner, $"learning/surveys/{setup.ItemId}/start", player, new Dictionary<string, string>
        {
            ["q.again"] = "yes",
        });
        var after = await learner.GetStringAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("You answered on", after);
        Assert.DoesNotContain("Change your answers", after);
    }

    [Fact]
    public async Task TheLegacyCourseSurveyStep_MovesTheQuestions_AndTakesTheOldPartOnlyAfterwards()
    {
        // Arrange: a course survey of the first release, with its questions in the old part and no survey yet.
        var itemId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Legacy survey");
            var item = await NewItemAsync(contentManager, course, LearningSurveysConstants.ContentTypes.FeedbackSurvey, "Old feedback", CompletionModes.Submitted);

            item.Alter<CourseSurveyPart>(_ => { });
            await contentManager.CreateAsync(item, VersionOptions.Published);

            return item.ContentItemId;
        });

        await _fixture.InTenantAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var version in await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == itemId).ListAsync())
            {
                var content = (JsonObject)version.Content;

                content.Remove(nameof(CourseSurveyPart));
                content[LegacyCourseSurveyUpgrade.LegacyPartName] = new JsonObject
                {
                    ["Questions"] = new JsonArray(new JsonObject { ["Text"] = "Was the pace right?", ["Type"] = 0, ["Options"] = new JsonArray("Yes", "No") }),
                };
                await session.SaveAsync(version);
            }
        });

        // Act
        await _fixture.InTenantAsync(async services =>
        {
            var step = services.GetServices<IResumableStep>().Single(candidate => candidate.Name == LegacyCourseSurveyUpgradeStep.StepName);
            var context = new ResumableStepContext();

            for (var run = 0; run < 1000 && !await step.RunAsync(context, TestContext.Current.CancellationToken); run++)
            {
            }
        });

        var (item, survey) = await _fixture.InTenantAsync(async services =>
        {
            var latest = await services.GetRequiredService<IContentManager>().GetAsync(itemId, VersionOptions.Latest);
            var owned = latest.TryGet<CourseSurveyPart>(out var part) && !string.IsNullOrEmpty(part.SurveyId)
                ? await services.GetRequiredService<ISurveyManager>().FindByIdAsync(part.SurveyId)
                : null;

            return (latest, owned);
        });

        // Assert
        Assert.Null(((JsonObject)item.Content)[LegacyCourseSurveyUpgrade.LegacyPartName]);
        Assert.NotNull(survey);
        Assert.Contains(survey.Draft.Pages.SelectMany(page => page.Questions), question => question.Title == "Was the pace right?");
    }

    private static SurveyDefinition Feedback()
        => LearningSurveyTestSupport.Definition("Course feedback", LearningSurveyTestSupport.SingleChoice("again", "Would you take it again?", ("yes", "Yes"), ("no", "No")));

    private Task<(SurveyResponse[] Responses, CourseItemProgress Progress)> ReadProgressAsync(string surveyId, string enrollmentId, string quizId)
        => _fixture.InTenantAsync(async services =>
        {
            var item = (await services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId))?.Find(quizId);
            var list = await services.GetRequiredService<ILearningSurveyService>().ListEnrollmentResponsesAsync(surveyId, enrollmentId);

            return (list.ToArray(), item);
        });

    private async Task<(CourseItemProgress Progress, SurveyResponse[] Responses)> ReadAsync((string CourseId, string QuizId, string SurveyId, string EnrollmentId) setup)
    {
        var (responses, progress) = await ReadProgressAsync(setup.SurveyId, setup.EnrollmentId, setup.QuizId);

        return (progress, responses);
    }

    private Task<(string CourseId, string QuizId, string SurveyId, string EnrollmentId)> CreateQuizCourseAsync(Action<SurveyDefinition> configureDefinition, Action<Survey> configureSurvey)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await NewCourseAsync(contentManager, "Quiz rules");
            var quiz = await NewItemAsync(contentManager, course, AssessmentsConstants.ContentTypes.Quiz, "Planets quiz", CompletionModes.Passed);

            quiz.Alter<QuizPart>(part => part.GradingMethod = GradingMethod.Highest);
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var surveyId = LearningSurveyTestSupport.SurveyIdOf(quiz);
            var definition = LearningSurveyTestSupport.Definition(
                quiz.DisplayText,
                LearningSurveyTestSupport.SingleChoice("planet", "Which planet is red?", ("venus", "Venus"), ("mars", "Mars")));

            definition.Pages[0].Questions[0].Scoring = new SurveyQuestionScoring { Points = 1, CorrectChoiceIds = ["mars"] };
            definition.Scoring.Enabled = true;
            definition.Scoring.PassMarkPercent = 50;
            configureDefinition(definition);
            await LearningSurveyTestSupport.PublishAsync(services, surveyId, definition, configureSurvey);

            var enrollmentId = await PublishAndEnrollAsync(services, contentManager, course, [quiz.ContentItemId]);

            return (course.ContentItemId, quiz.ContentItemId, surveyId, enrollmentId);
        });

    private static async Task<ContentItem> NewCourseAsync(IContentManager contentManager, string title)
    {
        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);

        course.DisplayText = title + " " + Guid.NewGuid().ToString("n")[..6];
        course.Alter<TitlePart>(part => part.Title = course.DisplayText);
        course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        return course;
    }

    private static async Task<ContentItem> NewItemAsync(IContentManager contentManager, ContentItem course, string contentType, string title, string completionMode)
    {
        var item = await contentManager.NewAsync(contentType);

        item.DisplayText = title + " " + Guid.NewGuid().ToString("n")[..6];
        item.Alter<TitlePart>(part => part.Title = item.DisplayText);
        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        item.Alter<CourseItemPart>(part => part.CompletionMode = completionMode);

        return item;
    }

    private async Task<string> PublishAndEnrollAsync(IServiceProvider services, IContentManager contentManager, ContentItem course, string[] itemIds)
    {
        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection { Id = "s1", Title = "Items", Items = [.. itemIds.Select(id => new CourseOutlineItem { ContentItemId = id })] },
        ]);
        await contentManager.UpdateAsync(course);
        await contentManager.PublishAsync(course);

        var enrolled = await services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = _fixture.EnrolledUserId,
            CourseId = course.ContentItemId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "quiz-panel-rules-" + Guid.NewGuid().ToString("n")[..6],
        });

        Assert.True(enrolled.Succeeded, enrolled.ErrorCode);

        return enrolled.Enrollment.ItemId;
    }
}
