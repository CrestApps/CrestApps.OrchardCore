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
/// The course's surveys filter the query, the source pages past one read, and the count is the real total.
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

    private static async Task<AssessmentTestHarness> CreateAsync()
    {
        var surveyManager = new Mock<ISurveyManager>();
        surveyManager
            .Setup(manager => manager.ListByOwnerAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordRef owner, CancellationToken _) => owner.Id switch
            {
                "quiz1" => [new Survey { ItemId = "survey1", Kind = SurveyKinds.Quiz, Owner = owner }],
                "quiz2" => [new Survey { ItemId = "survey2", Kind = SurveyKinds.Quiz, Owner = owner }],
                _ => [],
            });

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
