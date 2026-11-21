using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Learning.Surveys.Core.Services;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Learning.Surveys.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Remaining work section 3, item 13: the feedback survey and the choice (Moodle's Feedback and Choice activities) as
/// one survey course item. A learner answers once under their own active enrollment (again only when the survey allows
/// it), the answers are checked against the questions, the item completes on the answer, and staff see totals.
/// </summary>
public sealed class SurveyResponseServiceTests
{
    private const string Course = "course1";
    private const string Survey = "survey1";

    [Fact]
    public async Task Answer_IsKept_UnderTheLearnersEnrollment_AndCompletesTheItem()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await EnrollAsync(h, "ana");

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["4"]), ("q2", ["Yes"]), ("q3", ["Clear and short."]));
        var progress = await ProgressAsync(h, ana.ItemId);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ana.ItemId, result.Response.EnrollmentId);
        Assert.Equal(Course, result.Response.CourseId);
        Assert.Equal(["4"], result.Response.Answers["q1"]);
        Assert.Equal(CourseItemState.Completed, progress.State);
    }

    [Fact]
    public async Task Answer_ByALearnerWhoIsNotEnrolled_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ben");

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["4"]), ("q2", ["Yes"]));

        // Assert
        Assert.Equal(SurveyErrors.NotEnrolled, result.Error);
    }

    [Fact]
    public async Task Answer_LeavingARequiredQuestionOut_IsRefused_AndNamesIt()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["4"]));

        // Assert
        Assert.Equal(SurveyErrors.Required, result.Error);
        Assert.Equal("q2", result.QuestionId);
    }

    [Theory]
    [InlineData("q1", "6")]
    [InlineData("q1", "four")]
    [InlineData("q2", "Maybe")]
    public async Task Answer_OutsideTheQuestionsChoices_IsRefused(string questionId, string value)
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");
        var answers = new Dictionary<string, string[]>(StringComparer.Ordinal) { ["q1"] = ["3"], ["q2"] = ["No"] };
        answers[questionId] = [value];

        // Act
        var result = await AnswerAsync(h, "ana", [.. answers.Select(pair => (pair.Key, pair.Value))]);

        // Assert
        Assert.Equal(SurveyErrors.InvalidAnswer, result.Error);
        Assert.Equal(questionId, result.QuestionId);
    }

    [Fact]
    public async Task Answer_TwoChoicesToASingleChoiceQuestion_IsRefused_ButAMultipleChoiceQuestionTakesThem()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");
        await EnrollAsync(h, "ben");

        // Act
        var single = await AnswerAsync(h, "ana", ("q1", ["3"]), ("q2", ["Yes", "No"]));
        var multiple = await AnswerAsync(h, "ben", ("q1", ["3"]), ("q2", ["Yes"]), ("q4", ["Videos", "Quizzes"]));

        // Assert
        Assert.Equal(SurveyErrors.InvalidAnswer, single.Error);
        Assert.True(multiple.Succeeded, multiple.Error);
        Assert.Equal(["Videos", "Quizzes"], multiple.Response.Answers["q4"]);
    }

    [Fact]
    public async Task Answer_Again_IsRefused_UnlessTheSurveyLetsLearnersChangeIt_ThenItReplacesTheFirst()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");
        await AnswerAsync(h, "ana", ("q1", ["4"]), ("q2", ["Yes"]));

        // Act
        var refused = await AnswerAsync(h, "ana", ("q1", ["2"]), ("q2", ["No"]));
        h.Courses.Get(Survey).Alter<SurveyPart>(part => part.AllowChangeAnswer = true);
        var changed = await AnswerAsync(h, "ana", ("q1", ["2"]), ("q2", ["No"]));
        var all = await h.RunAsync(services => services.GetRequiredService<SurveyResponseStore>().PageBySurveyAsync(Survey, 0, 10));

        // Assert
        Assert.Equal(SurveyErrors.AlreadyAnswered, refused.Error);
        Assert.True(changed.Succeeded, changed.Error);
        Assert.Equal(1, all.Count);
        Assert.Equal(["2"], all.Items[0].Answers["q1"]);
    }

    [Fact]
    public async Task Answer_AfterTheSurveyClosed_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync(part => part.CloseUtc = LearningTestHarness.Now.AddHours(-1));
        await EnrollAsync(h, "ana");

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["4"]), ("q2", ["Yes"]));

        // Assert
        Assert.Equal(SurveyErrors.Closed, result.Error);
    }

    [Fact]
    public async Task Summary_CountsEachChoice_AveragesRatings_AndListsTheWrittenAnswers()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");
        await EnrollAsync(h, "ben");
        await EnrollAsync(h, "cid");
        await AnswerAsync(h, "ana", ("q1", ["5"]), ("q2", ["Yes"]), ("q3", ["Great"]));
        await AnswerAsync(h, "ben", ("q1", ["3"]), ("q2", ["Yes"]));
        await AnswerAsync(h, "cid", ("q1", ["4"]), ("q2", ["No"]), ("q3", ["Too long"]));

        // Act
        var summary = await h.RunAsync(services => services.GetRequiredService<ISurveyResponseService>().SummarizeAsync(Survey));

        // Assert
        Assert.Equal(3, summary.ResponseCount);
        var rating = summary.Questions.Single(question => question.QuestionId == "q1");
        Assert.Equal(4m, rating.Average);
        var choice = summary.Questions.Single(question => question.QuestionId == "q2");
        Assert.Equal(2, choice.Counts["Yes"]);
        Assert.Equal(1, choice.Counts["No"]);
        var text = summary.Questions.Single(question => question.QuestionId == "q3");
        Assert.Equal(["Great", "Too long"], text.TextAnswers.Order());
    }

    [Theory]
    [InlineData(SurveyResultsVisibility.Never, true, false, false)]
    [InlineData(SurveyResultsVisibility.AfterAnswer, false, false, false)]
    [InlineData(SurveyResultsVisibility.AfterAnswer, true, false, true)]
    [InlineData(SurveyResultsVisibility.AfterClose, true, false, false)]
    [InlineData(SurveyResultsVisibility.AfterClose, false, true, true)]
    [InlineData(SurveyResultsVisibility.Always, false, false, true)]
    public void LearnersSeeTheTotals_OnlyAsTheSurveySays(SurveyResultsVisibility visibility, bool answered, bool closed, bool expected)
    {
        // Arrange
        var part = new SurveyPart { ResultsVisibility = visibility, CloseUtc = closed ? LearningTestHarness.Now.AddDays(-1) : LearningTestHarness.Now.AddDays(1) };

        // Act
        var canSee = SurveyRules.LearnerSeesResults(part, answered, LearningTestHarness.Now);

        // Assert
        Assert.Equal(expected, canSee);
    }

    [Fact]
    public async Task Round11C1_Answer_BeforeTheCourseStarts_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        await EnrollAsync(h, "ana");
        h.Courses.Get(Course).Alter<CoursePart>(part => part.StartsUtc = LearningTestHarness.Now.AddDays(3));

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["4"]), ("q2", ["Yes"]));

        // Assert
        Assert.Equal(SurveyErrors.NotAvailableYet, result.Error);
    }

    [Theory]
    [InlineData("signed-out", false)]
    [InlineData("not-enrolled", false)]
    [InlineData("locked", false)]
    [InlineData("suspended", false)]
    [InlineData("enrolled", true)]
    [InlineData("completed", true)]
    [InlineData("staff", true)]
    public async Task Round11S2_TheTotals_AreOnlyForCourseStaff_AndLearnersTheItemIsOpenTo(string viewer, bool expected)
    {
        // Arrange: a survey that shows its totals to everyone, already answered by another learner.
        await using var h = await CreateAsync(part => part.ResultsVisibility = SurveyResultsVisibility.Always);
        await EnrollAsync(h, "ben");
        await AnswerAsync(h, "ben", ("q1", ["5"]), ("q2", ["Yes"]), ("q3", ["Private remark"]));

        var userId = viewer switch
        {
            "signed-out" => null,
            "staff" => "teacher",
            _ => "ana",
        };

        switch (viewer)
        {
            case "staff":
                h.Courses.Get(Course).Alter<CoursePart>(part => part.Staff = [new CourseStaffMember { UserId = "teacher", Role = LearningConstants.CourseStaffRoles.Instructor }]);
                break;
            case "locked":
                await EnrollAsync(h, "ana");
                h.Courses.Get(Survey).Alter<CourseItemPart>(part => part.ReleaseRules =
                [
                    new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "lesson1" } },
                ]);
                break;
            case "completed":
            case "suspended":
                var suspended = await EnrollAsync(h, "ana");
                await h.RunAsync(async services =>
                {
                    var store = services.GetRequiredService<EnrollmentStore>();
                    var enrollment = await store.FindByIdAsync(suspended.ItemId);

                    enrollment.Status = viewer == "completed" ? EnrollmentStatus.Completed : EnrollmentStatus.Suspended;
                    await store.SaveAsync(enrollment);

                    return true;
                });
                break;
            case "enrolled":
                await EnrollAsync(h, "ana");
                break;
        }

        // Act
        var canSee = await h.RunAsync(services => services.GetRequiredService<ISurveyResponseService>().CanSeeResultsAsync(Survey, userId));

        // Assert
        Assert.Equal(expected, canSee);
    }

    [Fact]
    public async Task Answer_AfterTheLearnerCompletedTheCourse_IsKept()
    {
        // Arrange: the end-of-course feedback is answered once the course is completed, its main use.
        await using var h = await CreateAsync();
        var ana = await EnrollAsync(h, "ana");
        await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(ana.ItemId, EnrollmentStatus.Completed, "admin", "Completed"));

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["5"]), ("q2", ["Yes"]));
        var found = await h.RunAsync(services => services.GetRequiredService<ISurveyResponseService>().FindAsync(ana.ItemId, Survey));

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ana.ItemId, result.Response.EnrollmentId);
        Assert.NotNull(found);
    }

    [Fact]
    public async Task Answer_AfterACompletedCoursesAccessEnded_IsRefused()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await EnrollAsync(h, "ana");
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(ana.ItemId);

            enrollment.Status = EnrollmentStatus.Completed;
            enrollment.AccessEndsUtc = LearningTestHarness.Now.AddDays(-1);
            await store.SaveAsync(enrollment);

            return true;
        });

        // Act
        var result = await AnswerAsync(h, "ana", ("q1", ["5"]), ("q2", ["Yes"]));

        // Assert
        Assert.Equal(SurveyErrors.AccessEnded, result.Error);
    }

    [Fact]
    public void TheSurveyKind_CompletesOnAnswer_OrWhenViewed_AndGivesNoGrade()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningSurveysCore();

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<IOptions<CourseItemKindOptions>>().Value.Find(SurveysConstants.SurveyKind);

        // Assert
        Assert.Equal(CompletionModes.Submitted, kind.DefaultCompletionMode);
        Assert.True(kind.Supports(CompletionModes.Viewed));
        Assert.False(kind.ProducesGrade);
    }

    private static async Task<SurveyHarness> CreateAsync(Action<SurveyPart> configure = null)
    {
        var clock = new TestClock { UtcNow = LearningTestHarness.Now };
        InMemoryCourses courses = null;

        var (harness, created, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddLearningSurveysCore();
                services.RemoveAll<ISurveyContentSource>();
                services.AddSingleton<ISurveyContentSource>(_ => new CoursesSurveySource(courses));
            },
            SurveysIndexSchema.CreateAsync,
            [new SurveyResponseIndexProvider()],
            [SurveysConstants.CollectionName]);

        courses = created;
        courses.AddCourse(Course);
        courses.AddItem(Course, "lesson1");

        var survey = courses.AddItem(Course, Survey, kind: SurveysConstants.SurveyKind, completionMode: CompletionModes.Submitted);

        survey.Alter<SurveyPart>(part =>
        {
            part.Questions =
            [
                new SurveyQuestion { Id = "q1", Text = "How clear was the course?", Type = SurveyQuestionType.Rating, Required = true },
                new SurveyQuestion { Id = "q2", Text = "Would you recommend it?", Type = SurveyQuestionType.SingleChoice, Options = ["Yes", "No"], Required = true },
                new SurveyQuestion { Id = "q3", Text = "What should change?", Type = SurveyQuestionType.Text },
                new SurveyQuestion { Id = "q4", Text = "What helped?", Type = SurveyQuestionType.MultipleChoice, Options = ["Videos", "Quizzes", "Handouts"] },
            ];
            configure?.Invoke(part);
        });

        return new SurveyHarness(harness, courses);
    }

    private static async Task<Enrollment> EnrollAsync(SurveyHarness h, string userId)
        => (await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = Course,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }))).Enrollment;

    private static Task<SurveyResponseResult> AnswerAsync(SurveyHarness h, string userId, params (string QuestionId, string[] Values)[] answers)
        => h.RunAsync(services => services.GetRequiredService<ISurveyResponseService>().AnswerAsync(new SurveyAnswerRequest
        {
            UserId = userId,
            SurveyId = Survey,
            Answers = answers.ToDictionary(answer => answer.QuestionId, answer => (IList<string>)answer.Values, StringComparer.Ordinal),
        }));

    private static async Task<CourseItemProgress> ProgressAsync(SurveyHarness h, string enrollmentId)
        => (await h.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollmentId)))?.Find(Survey);

    private sealed class CoursesSurveySource : ISurveyContentSource
    {
        private readonly InMemoryCourses _courses;

        public CoursesSurveySource(InMemoryCourses courses)
        {
            _courses = courses;
        }

        public Task<ContentItem> GetPublishedAsync(string contentItemId)
        {
            try
            {
                return Task.FromResult(_courses.Get(contentItemId));
            }
            catch (KeyNotFoundException)
            {
                return Task.FromResult<ContentItem>(null);
            }
        }
    }
}

internal sealed class SurveyHarness : IAsyncDisposable
{
    public SurveyHarness(CalendarTestHarness harness, InMemoryCourses courses)
    {
        Harness = harness;
        Courses = courses;
    }

    public CalendarTestHarness Harness { get; }

    public InMemoryCourses Courses { get; }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Harness.RunAsync(work);

    public ValueTask DisposeAsync()
        => Harness.DisposeAsync();
}
