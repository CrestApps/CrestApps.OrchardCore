using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// How a Moodle quiz behaves: several questions per page, immediate feedback that checks and locks a question, review
/// options per moment, the order the author set, and ungraded questions.
/// </summary>
public sealed class QuizBehaviourTests
{
    [Fact]
    public async Task Pages_SplitTheQuestions_AndAFreeQuizMovesToAnyPage()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 5);
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1", "q2", "q3", "q4", "q5"];
            quiz.QuestionsPerPage = 2;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        var moved = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 2));
        var back = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 0));
        var answer = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 4, new AttemptResponse { ChoiceIds = ["a"] }));
        var tooFar = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 3));

        // Assert
        Assert.Equal(2, attempt.QuestionsPerPage);
        Assert.Equal(3, AttemptPaging.PageCount(attempt, sequential: false));
        Assert.Equal(["q3", "q4"], AttemptPaging.ItemsOn(attempt, 1, sequential: false).Select(item => item.QuestionId));
        Assert.Equal(["q5"], AttemptPaging.ItemsOn(attempt, 2, sequential: false).Select(item => item.QuestionId));
        Assert.Equal(2, moved.Attempt.CurrentPage);
        Assert.Equal(0, back.Attempt.CurrentPage);
        Assert.True(answer.Succeeded, answer.ErrorCode);
        Assert.Equal(AttemptErrors.NotFound, tooFar.ErrorCode);
    }

    [Fact]
    public async Task ASequentialQuiz_TakesAnswersOnItsCurrentPageOnly_AndNeverGoesBack()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 4);
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1", "q2", "q3", "q4"];
            quiz.QuestionsPerPage = 2;
            quiz.Navigation = QuizNavigation.Sequential;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        var ahead = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 2, new AttemptResponse { ChoiceIds = ["a"] }));
        var first = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["b"] }));
        var changed = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        var second = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { ChoiceIds = ["a"] }));
        var skip = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 0));
        var next = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 1));
        var back = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["b"] }));
        var backPage = await h.RunAsync(service => service.GoToPageAsync(attempt.ItemId, "learner", 0));

        // Assert
        Assert.Equal(AttemptErrors.ItemLocked, ahead.ErrorCode);
        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(changed.Succeeded, changed.ErrorCode);
        Assert.True(second.Succeeded, second.ErrorCode);
        Assert.Equal(AttemptErrors.ItemLocked, skip.ErrorCode);
        Assert.Equal(1, next.Attempt.CurrentPage);
        Assert.Equal(AttemptErrors.ItemLocked, back.ErrorCode);
        Assert.Equal(AttemptErrors.ItemLocked, backPage.ErrorCode);
        Assert.Equal(["a"], next.Attempt.Items[0].Response.ChoiceIds);
    }

    [Fact]
    public async Task ASequentialQuizWithoutAPageSize_ShowsOneQuestionAtATime()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 3);
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1", "q2", "q3"];
            quiz.Navigation = QuizNavigation.Sequential;
        });
        await h.EnrollAsync("learner", "course1");

        // Act
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Assert
        Assert.Equal(3, AttemptPaging.PageCount(attempt, sequential: true));
        Assert.Equal(1, AttemptPaging.PageCount(attempt, sequential: false));
        Assert.Equal(["q2"], AttemptPaging.ItemsOn(attempt, 1, sequential: true).Select(item => item.QuestionId));
    }

    [Fact]
    public async Task ImmediateFeedback_ChecksAQuestion_ScoresIt_AndLocksIt()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 2);
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1", "q2"];
            quiz.FeedbackMode = QuizFeedbackMode.Immediate;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        var checkedResult = await h.RunAsync(service => service.CheckAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["b"] }));
        var change = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        var again = await h.RunAsync(service => service.CheckAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        var submitted = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        // Assert
        Assert.True(checkedResult.Succeeded, checkedResult.ErrorCode);
        var item = checkedResult.Attempt.Items[0];
        Assert.NotNull(item.CheckedUtc);
        Assert.Equal(0m, item.AutoScore);
        Assert.True(checkedResult.Attempt.IsOpen);
        Assert.Equal(AttemptErrors.ItemLocked, change.ErrorCode);
        Assert.Equal(AttemptErrors.ItemLocked, again.ErrorCode);
        Assert.Equal(["b"], submitted.Attempt.Items[0].Response.ChoiceIds);
        Assert.Equal(0m, submitted.Attempt.Score);
    }

    [Fact]
    public async Task Check_IsRefused_WhenTheQuizDefersFeedback()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 1);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        var result = await h.RunAsync(service => service.CheckAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));

        // Assert
        Assert.Equal(AttemptErrors.CheckNotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task ReviewOptions_ShowWhatEachMomentAllows()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        AddChoices(h, 1);
        var closes = h.Clock.UtcNow.AddDays(2);
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.ClosesUtc = closes;
            quiz.FeedbackMode = QuizFeedbackMode.Immediate;
            quiz.ReviewOptions = new QuizReviewOptions
            {
                During = ReviewShows.Score | ReviewShows.Feedback,
                ImmediatelyAfter = ReviewShows.All,
                LaterWhileOpen = ReviewShows.Score,
                AfterClose = ReviewShows.Score | ReviewShows.RightAnswers | ReviewShows.GeneralFeedback,
            };
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        var checkedAttempt = (await h.RunAsync(service => service.CheckAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }))).Attempt;
        var submitted = (await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"))).Attempt;

        Task<ReviewShows> Shows(AssessmentAttempt target) => h.RunAsync(services => Task.FromResult(services.GetRequiredService<AttemptReviewPolicy>().Shows(target)));
        Task<ReviewShows> ShowsFor(AssessmentAttempt target, int index) => h.RunAsync(services => Task.FromResult(services.GetRequiredService<AttemptReviewPolicy>().ShowsFor(target, target.Items[index])));

        // Act and assert
        Assert.Equal(ReviewShows.Score | ReviewShows.Feedback, await ShowsFor(checkedAttempt, 0));
        Assert.Equal(ReviewShows.None, await ShowsFor(attempt, 0));

        h.Clock.UtcNow = submitted.SubmittedUtc.Value.AddMinutes(1);
        Assert.Equal(ReviewShows.All, await Shows(submitted));

        h.Clock.UtcNow = submitted.SubmittedUtc.Value.AddMinutes(10);
        Assert.Equal(ReviewShows.Score, await Shows(submitted));

        h.Clock.UtcNow = closes.AddMinutes(1);
        Assert.Equal(ReviewShows.Score | ReviewShows.RightAnswers | ReviewShows.GeneralFeedback, await Shows(submitted));
    }

    [Theory]
    [InlineData(ReviewPolicy.AfterGraded, ReviewShows.All, ReviewShows.All, ReviewShows.All)]
    [InlineData(ReviewPolicy.AfterClose, ReviewShows.Score, ReviewShows.Score, ReviewShows.All)]
    [InlineData(ReviewPolicy.ScoreOnly, ReviewShows.Score, ReviewShows.Score, ReviewShows.Score)]
    [InlineData(ReviewPolicy.Hidden, ReviewShows.None, ReviewShows.None, ReviewShows.None)]
    public void TheReviewPolicy_StaysTheDefault_AsReviewOptions(ReviewPolicy policy, ReviewShows immediately, ReviewShows later, ReviewShows afterClose)
    {
        // Act
        var options = QuizReviewOptions.FromPolicy(policy);

        // Assert
        Assert.Equal(immediately, options.ImmediatelyAfter);
        Assert.Equal(later, options.LaterWhileOpen);
        Assert.Equal(afterClose, options.AfterClose);
    }

    [Fact]
    public async Task AnAttemptWithoutReviewOptions_FollowsItsReviewPolicy()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var policy = await h.RunAsync(services => Task.FromResult(services.GetRequiredService<AttemptReviewPolicy>()));
        var graded = new AssessmentAttempt { Status = AttemptStatus.Graded, SubmittedUtc = h.Clock.UtcNow.AddDays(-1), ReviewPolicy = ReviewPolicy.ScoreOnly };

        // Act and assert
        Assert.True(policy.CanSeeScore(graded));
        Assert.False(policy.CanReview(graded));
        Assert.False(policy.CanSeeScore(new AssessmentAttempt { Status = AttemptStatus.NeedsGrading, ReviewPolicy = ReviewPolicy.AfterGraded }));
    }

    [Fact]
    public async Task FixedQuestions_KeepTheOrderTheAuthorSet()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("zebra", "a");
        h.Questions.AddChoice("apple", "a");
        h.Questions.AddChoice("mango", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["zebra", "apple", "mango"]);
        await h.EnrollAsync("learner", "course1");

        // Act
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Assert
        Assert.Equal(["zebra", "apple", "mango"], attempt.Items.Select(item => item.QuestionId));
    }

    [Fact]
    public async Task DescriptionsAndUngradedSurveys_AddNoPoints_AndNeverBlockTheGrade()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddQuestion("intro", AssessmentsConstants.ContentTypes.DescriptionQuestion, points: 0);
        h.Questions.AddChoice("q1", "a", points: 2);
        h.Questions.AddQuestion("opinion", AssessmentsConstants.ContentTypes.SurveyQuestion, points: 0, question => question.Alter<SurveyQuestionPart>(part => part.Options = [new ChoiceOption { Id = "good", Text = "Good" }, new ChoiceOption { Id = "bad", Text = "Bad" }]));
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["intro", "q1", "opinion"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { ChoiceIds = ["a"] }));
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 2, new AttemptResponse { ChoiceIds = ["bad"] }));
        var description = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { Texts = ["x"] }));
        var submitted = (await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"))).Attempt;

        // Assert
        Assert.Equal(AttemptErrors.ItemLocked, description.ErrorCode);
        Assert.Equal(2m, submitted.MaxScore);
        Assert.Equal(2m, submitted.Score);
        Assert.Equal(100m, submitted.Percent);
        Assert.Equal(AttemptStatus.Graded, submitted.Status);
    }

    private static void AddChoices(AssessmentTestHarness h, int count)
    {
        for (var i = 1; i <= count; i++)
        {
            h.Questions.AddChoice("q" + i, "a");
        }
    }
}
