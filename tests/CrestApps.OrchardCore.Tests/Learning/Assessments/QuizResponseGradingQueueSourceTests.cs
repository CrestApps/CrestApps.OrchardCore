using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// R3-H3: the quiz attempts of a course stay in the grading list however many attempts wait elsewhere on the site.
/// The course's surveys filter the query, the source pages past one read, and the count is the real total. R4-L5:
/// attempts of unpublished courses and items stay listed, the surveys are looked up in one query, the survey ids are
/// read in chunks under SQL Server's parameter limit, and a deep page doesn't read every waiting attempt.
/// </summary>
public sealed class QuizResponseGradingQueueSourceTests
{
    [Fact]
    public async Task AnOldAttempt_StillAppears_WhenManyNewerAttemptsWaitInAnotherCourse()
    {
        // Arrange: one old attempt in course1, then more newer attempts in course2 than one read returns.
        await using var h = await CreateAsync();
        await AddAsync(h, "old", "survey1", "course1", LearningTestHarness.Now.AddDays(-30));

        for (var i = 0; i < SurveysConstants.PageSize + 20; i++)
        {
            await AddAsync(h, "other-" + i, "survey2", "course2", LearningTestHarness.Now.AddMinutes(i));
        }

        // Act
        var page = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course1"], 1, 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, page.Count);
        Assert.Equal("old", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task MoreThanOneReadOfPendingAttempts_PagesOldestFirst_AndCountsThemAll()
    {
        // Arrange: more pending attempts in course1 than one read returns.
        await using var h = await CreateAsync();
        var total = SurveysConstants.PageSize + 30;

        for (var i = 0; i < total; i++)
        {
            await AddAsync(h, "attempt-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), "survey1", "course1", LearningTestHarness.Now.AddMinutes(i));
        }

        // Act: the last page reaches past the first read.
        var pageSize = 50;
        var lastPage = (total + pageSize - 1) / pageSize;
        var first = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course1"], 1, pageSize, TestContext.Current.CancellationToken));
        var last = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course1"], lastPage, pageSize, TestContext.Current.CancellationToken));
        var direct = await h.RunAsync(services => services.GetServices<IGradingQueueSource>().OfType<QuizResponseGradingQueueSource>().Single()
            .ListWaitingAsync(new GradingQueueQuery { CourseIds = ["course1"], Take = total }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(total, first.Count);
        Assert.Equal("attempt-0000", first.Items[0].Id);
        Assert.Equal("attempt-" + (total - 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture), last.Items[^1].Id);
        Assert.Equal(total, direct.Count);
        Assert.Equal(total, direct.Items.Count);
        Assert.Equal(direct.Items.OrderBy(item => item.SubmittedUtc).Select(item => item.Id), direct.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task AttemptsOfAnUnpublishedCourseAndItem_StayInTheCoursesList()
    {
        // Arrange: a course and its quiz were unpublished (or never published) after a learner's attempt.
        await using var h = await CreateAsync();
        h.Courses.AddCourse("course3").Published = false;
        h.AddQuiz("course3", "quiz3", null).Published = false;
        await AddAsync(h, "draft-attempt", "survey3", "course3", LearningTestHarness.Now.AddDays(-1));

        // Act
        var page = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course3"], 1, 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, page.Count);
        Assert.Equal("draft-attempt", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task ACourseWithMoreSurveysThanOneInList_IsReadInChunks_LookedUpOnce_AndMergedOldestFirst()
    {
        // Arrange: one course whose items own more surveys than one IN list holds, with attempts in the first and the
        // last chunk.
        var surveyManager = SurveyManager();
        await using var h = await CreateAsync(surveyManager);
        h.Courses.AddCourse("big");
        var surveys = QuizResponseGradingQueueSource.SurveysPerQuery + 200;

        for (var i = 0; i < surveys; i++)
        {
            h.AddQuiz("big", "quiz-big-" + i, null);
        }

        await AddAsync(h, "newer", "survey-big-5", "big", LearningTestHarness.Now.AddMinutes(10));
        await AddAsync(h, "older", "survey-big-" + (surveys - 1), "big", LearningTestHarness.Now);

        // Act
        var page = await h.RunAsync(services => Source(services).ListWaitingAsync(new GradingQueueQuery { CourseIds = ["big"], Take = 10 }, TestContext.Current.CancellationToken));
        var queries = await h.RunAsync(services => Task.FromResult(services.GetRequiredService<InMemorySurveyResponses>().Queries));

        // Assert
        Assert.Equal(2, page.Count);
        Assert.Equal(["older", "newer"], page.Items.Select(item => item.Id));
        Assert.True(queries.Count >= 2);
        Assert.All(queries, query => Assert.InRange(query.SurveyIds.Count, 1, QuizResponseGradingQueueSource.SurveysPerQuery));
        surveyManager.Verify(manager => manager.ListByOwnersAsync(It.IsAny<IReadOnlyCollection<RecordRef>>(), It.IsAny<CancellationToken>()), Times.Once());
        surveyManager.Verify(manager => manager.ListByOwnerAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task AVeryDeepPage_ReadsNoMoreThanTheCap()
    {
        // Arrange: more waiting attempts than the cap.
        await using var h = await CreateAsync();
        var total = QuizResponseGradingQueueSource.MaxTake + 50;

        for (var i = 0; i < total; i++)
        {
            await AddAsync(h, "attempt-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), "survey1", "course1", LearningTestHarness.Now.AddMinutes(i));
        }

        // Act: page 100,000 of 50.
        var page = await h.RunAsync(services => Source(services).ListWaitingAsync(new GradingQueueQuery { CourseIds = ["course1"], Take = 100_000 * 50 }, TestContext.Current.CancellationToken));
        var queries = await h.RunAsync(services => Task.FromResult(services.GetRequiredService<InMemorySurveyResponses>().Queries));

        // Assert: the count is still the real total, but only the cap was read.
        Assert.Equal(total, page.Count);
        Assert.Equal(QuizResponseGradingQueueSource.MaxTake, page.Items.Count);
        Assert.InRange(queries.Max(query => query.Skip + query.Take), 1, QuizResponseGradingQueueSource.MaxTake);
    }

    [Fact]
    public async Task P4M1_TheGradersOwnAttempt_IsNotListed()
    {
        // Arrange: a member of the course staff who also took the quiz.
        await using var h = await CreateAsync();
        var mine = await AddAsync(h, "mine", "survey1", "course1", LearningTestHarness.Now.AddMinutes(-10));
        await AddAsync(h, "theirs", "survey1", "course1", LearningTestHarness.Now.AddMinutes(-5));
        mine.UserId = "grader";

        // Act
        var page = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course1"], "grader", 1, 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, page.Count);
        Assert.Equal("theirs", Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task P2M3_AnAnonymousQuizSurvey_ListsNothing_WhileTheSmallGroupRuleCannotOpenIt()
    {
        // Arrange: quiz1's survey is anonymous; this site has no small-group rule to count it, so it never opens.
        var surveyManager = new Mock<ISurveyManager>();
        surveyManager
            .Setup(manager => manager.ListByOwnersAsync(It.IsAny<IReadOnlyCollection<RecordRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<RecordRef> owners, CancellationToken _) => owners
                .Where(owner => owner.Id == "quiz1")
                .Select(owner => new Survey
                {
                    ItemId = "survey1",
                    Kind = SurveyKinds.Quiz,
                    Owner = owner,
                    Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous },
                })
                .ToArray());
        await using var h = await CreateAsync(surveyManager);
        await AddAsync(h, "anonymous", "survey1", "course1", LearningTestHarness.Now.AddMinutes(-10));

        // Act
        var page = await h.RunAsync(services => services.GetRequiredService<GradingQueue>().PageAsync(["course1"], 1, 10, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(0, page.Count);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task P2M3_AnAnonymousAttemptOnTheEveryCourseList_NamesNobody_AndShowsNoResponseNumberOrTime()
    {
        // Arrange: the list of every course, where the survey of an attempt is looked up and is anonymous.
        var surveyManager = SurveyManager();
        surveyManager
            .Setup(manager => manager.FindByIdAsync("survey1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Survey { ItemId = "survey1", Kind = SurveyKinds.Quiz, Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } });
        await using var h = await CreateAsync(surveyManager);
        var response = await AddAsync(h, "01anonymousresponseid", "survey1", "course1", new DateTime(2026, 1, 10, 14, 37, 12, DateTimeKind.Utc));
        response.Number = "R0000042";

        // Act
        var page = await h.RunAsync(services => Source(services).ListWaitingAsync(new GradingQueueQuery { CourseIds = null, Take = 10 }, TestContext.Current.CancellationToken));

        // Assert
        var item = Assert.Single(page.Items);

        Assert.Null(item.UserId);
        Assert.NotEqual("R0000042", item.Reference);
        Assert.Equal("01anonymou", item.Reference);
        // The start of the day in the site's time zone: a whole hour before the real time, less than a day before.
        var submitted = new DateTime(2026, 1, 10, 14, 37, 12, DateTimeKind.Utc);

        Assert.Equal(0, item.SubmittedUtc.Minute);
        Assert.Equal(0, item.SubmittedUtc.Second);
        Assert.InRange(submitted - item.SubmittedUtc, TimeSpan.FromMinutes(1), TimeSpan.FromDays(1));
    }

    [Fact]
    public void VM5_AListThatShowsAnAnonymousSurveysAttempts_IsReadInRandomIdOrder()
    {
        // Arrange
        var named = new Survey { ItemId = "named", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified } };
        var anonymous = new Survey { ItemId = "anonymous", Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Anonymous } };

        // Act + Assert: only named surveys, or an anonymous one hidden by the small-group rule, keep oldest first.
        Assert.False(QuizResponseGradingQueueSource.ListsAnonymous([named], []));
        Assert.False(QuizResponseGradingQueueSource.ListsAnonymous([named, anonymous], ["anonymous"]));
        Assert.True(QuizResponseGradingQueueSource.ListsAnonymous([named, anonymous], []));
    }

    [Fact]
    public async Task VM5_ANamedOnlyList_StaysOldestFirst()
    {
        // Arrange
        await using var h = await CreateAsync();
        await AddAsync(h, "b-older", "survey1", "course1", LearningTestHarness.Now.AddMinutes(-10));
        await AddAsync(h, "a-newer", "survey1", "course1", LearningTestHarness.Now.AddMinutes(-5));

        // Act
        var page = await h.RunAsync(services => Source(services).ListWaitingAsync(new GradingQueueQuery { CourseIds = ["course1"], Take = 10 }, TestContext.Current.CancellationToken));
        var queries = await h.RunAsync(services => Task.FromResult(services.GetRequiredService<InMemorySurveyResponses>().Queries));

        // Assert
        Assert.Equal(["b-older", "a-newer"], page.Items.Select(item => item.Id));
        Assert.All(queries, query => Assert.False(query.OrderById));
    }

    private static QuizResponseGradingQueueSource Source(IServiceProvider services)
        => services.GetServices<IGradingQueueSource>().OfType<QuizResponseGradingQueueSource>().Single();

    private static Mock<ISurveyManager> SurveyManager()
    {
        // Each quiz owns one survey: quiz1 owns survey1, quiz-big-7 owns survey-big-7.
        var surveyManager = new Mock<ISurveyManager>();
        surveyManager
            .Setup(manager => manager.ListByOwnersAsync(It.IsAny<IReadOnlyCollection<RecordRef>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<RecordRef> owners, CancellationToken _) => owners
                .Where(owner => owner.Id.StartsWith("quiz", StringComparison.Ordinal))
                .Select(owner => new Survey { ItemId = string.Concat("survey", owner.Id.AsSpan("quiz".Length)), Kind = SurveyKinds.Quiz, Owner = owner })
                .ToArray());

        return surveyManager;
    }

    private static async Task<AssessmentTestHarness> CreateAsync(Mock<ISurveyManager> surveyManager = null)
    {
        surveyManager ??= SurveyManager();

        var h = await AssessmentTestHarness.CreateAsync(services => services.AddSingleton(surveyManager.Object));
        h.Courses.AddCourse("course1");
        h.Courses.AddCourse("course2");
        h.AddQuiz("course1", "quiz1", null);
        h.AddQuiz("course2", "quiz2", null);

        return h;
    }

    private static Task<SurveyResponse> AddAsync(AssessmentTestHarness h, string id, string surveyId, string courseId, DateTime completedUtc)
        => h.RunAsync(services =>
        {
            var response = new SurveyResponse
            {
                ItemId = id,
                SurveyId = surveyId,
                UserId = "learner",
                Status = SurveyResponseStatus.Completed,
                Grading = GradingStatus.Pending,
                Context = LearningSurveyContexts.Enrollment("enrollment-" + courseId),
                StartedUtc = completedUtc,
                CompletedUtc = completedUtc,
            };

            response.Properties[LearningSurveyContexts.CourseIdProperty] = courseId;
            services.GetRequiredService<InMemorySurveyResponses>().Add(response);

            return Task.FromResult(response);
        });
}
