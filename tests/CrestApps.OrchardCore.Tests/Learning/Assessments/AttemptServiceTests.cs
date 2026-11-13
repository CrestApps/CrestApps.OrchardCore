using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// The server runs each attempt: it draws and copies the questions, fixes the deadline, saves answers, accepts the
/// submit once, submits at the deadline, grades, regrades and voids, and reports the quiz result to Learning.
/// </summary>
public sealed class AttemptServiceTests
{
    [Fact]
    public async Task Start_DrawsFixedAndRandomQuestions_CopiesThem_FixesTheDeadline_AndReturnsTheSameAttemptUntilSubmitted()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("fixed", "a");
        h.Questions.AddChoice("bank-fire-1", "a", bankId: "bank", tag: "fire");
        h.Questions.AddChoice("bank-fire-2", "b", bankId: "bank", tag: "fire");
        h.Questions.AddChoice("bank-fire-3", "c", bankId: "bank", tag: "fire");
        h.Questions.AddChoice("bank-water", "a", bankId: "bank", tag: "water");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["fixed"];
            quiz.DrawRules = [new QuizDrawRule { BankId = "bank", Tag = "fire", Count = 2 }];
            quiz.TimeLimitMinutes = 20;
        });
        await h.EnrollAsync("learner", "course1");

        // Act
        var first = await h.StartAsync("learner", "quiz");
        var again = await h.StartAsync("learner", "quiz");

        // Assert
        Assert.True(first.Succeeded, first.ErrorCode);
        var attempt = first.Attempt;
        Assert.Equal(3, attempt.Items.Count);
        Assert.Equal("fixed", attempt.Items[0].QuestionId);
        Assert.All(attempt.Items.Skip(1), item => Assert.StartsWith("bank-fire-", item.QuestionId, StringComparison.Ordinal));
        Assert.Equal(2, attempt.Items.Skip(1).Select(item => item.QuestionId).Distinct().Count());
        Assert.All(attempt.Items, item => Assert.Single(item.Key.ChoiceIds));
        Assert.Equal("Prompt of fixed", attempt.Items[0].Prompt);
        Assert.Equal(LearningTestHarness.Now.AddMinutes(20), attempt.DeadlineUtc);
        Assert.Equal("A000001", attempt.Number);
        Assert.Equal(AttemptStatus.InProgress, attempt.Status);
        Assert.Equal(attempt.ItemId, again.Attempt.ItemId);
    }

    [Fact]
    public async Task Start_RefusesPeopleNotEnrolled_OutsideTheWindow_AndWithoutTheCodeOrHonor_UnlessAnOverrideOpensIt()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.ClosesUtc = LearningTestHarness.Now.AddHours(-1);
            quiz.HonorStatement = "I work alone.";
            quiz.AccessCodeHash = CrestApps.OrchardCore.Learning.Core.Services.EnrollmentKeys.Hash("ROOM-12");
        });
        await h.EnrollAsync("learner", "course1");

        // Act and assert
        Assert.Equal(AttemptErrors.NoAccess, (await h.StartAsync("stranger", "quiz")).ErrorCode);
        Assert.Equal(AttemptErrors.Closed, (await h.StartAsync("learner", "quiz", r => r.AccessCode = "ROOM-12")).ErrorCode);

        await h.RunAsync(async services =>
        {
            await services.GetRequiredService<AssessmentOverrideStore>().CreateAsync(new AssessmentOverride
            {
                UserId = "learner",
                QuizId = "quiz",
                ClosesUtc = LearningTestHarness.Now.AddDays(1),
                Reason = "Was ill on the day",
                Actor = "manager",
            });

            return true;
        });

        Assert.Equal(AttemptErrors.WrongAccessCode, (await h.StartAsync("learner", "quiz", r => r.AccessCode = "WRONG")).ErrorCode);
        Assert.Equal(AttemptErrors.HonorRequired, (await h.StartAsync("learner", "quiz", r =>
        {
            r.AccessCode = "ROOM-12";
            r.HonorConfirmed = false;
        })).ErrorCode);
        var started = await h.StartAsync("learner", "quiz", r => r.AccessCode = "ROOM-12");
        Assert.True(started.Succeeded, started.ErrorCode);
        Assert.Equal("I work alone.", started.Attempt.HonorStatement);
    }

    [Fact]
    public async Task Submit_ScoresAutomatically_ReportsThePassToLearning_AndASecondSubmitChangesNothing()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.Questions.AddChoice("q2", "b");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1", "q2"]);
        var enrollment = await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { ChoiceIds = ["c"] }));
        var stolen = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "intruder", 1, new AttemptResponse { ChoiceIds = ["b"] }));
        var submitted = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));
        var again = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        // Assert
        Assert.Equal(AttemptErrors.NotFound, stolen.ErrorCode);
        Assert.Equal(AttemptStatus.Graded, submitted.Attempt.Status);
        Assert.Equal(1m, submitted.Attempt.Score);
        Assert.Equal(50m, submitted.Attempt.Percent);
        Assert.True(submitted.Attempt.Passed);
        Assert.Equal(submitted.Attempt.SubmittedUtc, again.Attempt.SubmittedUtc);
        Assert.Single(again.Attempt.ScoreHistory);

        var progress = await h.ProgressAsync(enrollment.ItemId, "quiz");
        Assert.Equal(CourseItemState.Completed, progress.State);
        Assert.Equal(50m, progress.Score);
        Assert.True(progress.Passed);
    }

    [Fact]
    public async Task TheDeadline_RefusesLateAnswers_AndTheServerSubmitsAtTheDeadline()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1"];
            quiz.TimeLimitMinutes = 10;
        });
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));

        // Act
        var early = await h.RunAsync(service => service.SubmitAtDeadlineAsync(attempt.ItemId));
        h.Clock.UtcNow = LearningTestHarness.Now.AddMinutes(11);
        var late = await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["b"] }));
        var final = await h.RunAsync(async services => await services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(attempt.ItemId));

        // Assert
        Assert.Equal(AttemptStatus.InProgress, early.Attempt.Status);
        Assert.Equal(AttemptErrors.DeadlinePassed, late.ErrorCode);
        Assert.Equal(AttemptStatus.Graded, final.Status);
        Assert.True(final.AutoSubmitted);
        Assert.Equal(LearningTestHarness.Now.AddMinutes(10), final.SubmittedUtc);
        Assert.Equal(1m, final.Score);
    }

    [Fact]
    public async Task AnEssay_WaitsForAGrader_WhoseScoreIsCheckedAndKeptInTheHistory()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.Questions.AddEssay("essay", points: 4);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1", "essay"], completionMode: CompletionModes.Graded);
        var enrollment = await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { Texts = ["Heat, fuel and oxygen."] }));

        // Act
        var submitted = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));
        var notYet = await h.ProgressAsync(enrollment.ItemId, "quiz");
        var tooMuch = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [1] = new() { Points = 9 } }, null, "grader"));
        var graded = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [1] = new() { Points = 3, Comment = "Name the triangle." } }, "Good.", "grader"));

        // Assert
        Assert.Equal(AttemptStatus.NeedsGrading, submitted.Attempt.Status);
        Assert.NotEqual(CourseItemState.Completed, notYet?.State);
        Assert.Equal(AttemptErrors.InvalidScore, tooMuch.ErrorCode);
        Assert.Equal(AttemptStatus.Graded, graded.Attempt.Status);
        Assert.Equal(4m, graded.Attempt.Score);
        Assert.Equal(80m, graded.Attempt.Percent);
        Assert.Equal("grader", graded.Attempt.GradedBy);
        Assert.Equal(["Submitted", "Graded"], graded.Attempt.ScoreHistory.Select(change => change.Kind));
        Assert.Equal(CourseItemState.Completed, (await h.ProgressAsync(enrollment.ItemId, "quiz")).State);
    }

    [Fact]
    public async Task Attempts_AreLimited_SpacedByTheDelay_AndScoredByTheGradingMethod()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.Questions.AddChoice("q2", "a");
        h.AddQuiz("course1", "quiz", quiz =>
        {
            quiz.QuestionIds = ["q1", "q2"];
            quiz.MaxAttempts = 2;
            quiz.DelayMinutes = 30;
            quiz.GradingMethod = GradingMethod.Highest;
        }, completionMode: CompletionModes.Submitted, passMark: 100);
        h.Courses.AddItem("course1", "reading");
        var enrollment = await h.EnrollAsync("learner", "course1");

        // Act
        await TakeAsync(h, ["a", "a"]);
        var tooSoon = await h.StartAsync("learner", "quiz");
        h.Clock.UtcNow = LearningTestHarness.Now.AddMinutes(31);
        await TakeAsync(h, ["a", "b"]);
        h.Clock.UtcNow = LearningTestHarness.Now.AddHours(2);
        var noneLeft = await h.StartAsync("learner", "quiz");

        // Assert
        Assert.Equal(AttemptErrors.TooSoon, tooSoon.ErrorCode);
        Assert.Equal(LearningTestHarness.Now.AddMinutes(30), tooSoon.AvailableFromUtc);
        Assert.Equal(AttemptErrors.NoAttemptsLeft, noneLeft.ErrorCode);
        var progress = await h.ProgressAsync(enrollment.ItemId, "quiz");
        Assert.Equal(100m, progress.Score);
        Assert.True(progress.Passed);
        Assert.Equal(2, progress.Attempts);
    }

    [Fact]
    public async Task Regrade_AndVoid_NeedAReason_KeepTheOldScores_AndAVoidedAttemptStopsCounting()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"], completionMode: CompletionModes.Submitted);
        h.Courses.AddItem("course1", "reading");
        var enrollment = await h.EnrollAsync("learner", "course1");
        var attempt = await TakeAsync(h, ["b"]);

        // Act
        var noReason = await h.RunAsync(service => service.RegradeAsync(attempt.ItemId, "admin", " "));
        await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AssessmentAttemptStore>();
            var stored = await store.FindByIdAsync(attempt.ItemId);
            stored.Items[0].Key.ChoiceIds = ["b"];
            await store.UpdateAsync(stored);

            return true;
        });
        var regraded = await h.RunAsync(service => service.RegradeAsync(attempt.ItemId, "admin", "The key was wrong"));
        var voided = await h.RunAsync(service => service.VoidAsync(attempt.ItemId, "admin", "Taken by someone else"));

        // Assert
        Assert.Equal(AttemptErrors.ReasonRequired, noReason.ErrorCode);
        Assert.Equal(1m, regraded.Attempt.Score);
        Assert.Contains(regraded.Attempt.ScoreHistory, change => change.Kind == "Regraded" && change.From == 0m && change.To == 1m && change.Reason == "The key was wrong");
        Assert.Equal(AttemptStatus.Voided, voided.Attempt.Status);
        Assert.Contains(voided.Attempt.ScoreHistory, change => change.Kind == "Voided");
        Assert.Null((await h.ProgressAsync(enrollment.ItemId, "quiz")).Score);
    }

    [Fact]
    public async Task AnAttempt_ReadsTheSame_AfterItsQuestionIsGone()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddChoice("q1", "a");
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1"]);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;

        // Act
        h.Questions.Remove("q1");
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        var submitted = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));
        var regraded = await h.RunAsync(service => service.RegradeAsync(attempt.ItemId, "admin", "Check after pruning"));

        // Assert
        Assert.Equal(1m, submitted.Attempt.Score);
        Assert.Equal(1m, regraded.Attempt.Score);
        Assert.Equal("Prompt of q1", regraded.Attempt.Items[0].Prompt);
        Assert.Equal(3, regraded.Attempt.Items[0].Choices.Count);
    }

    [Fact]
    public async Task Review_IsAllowedOnlyAsThePolicySays()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var service = new AttemptReviewPolicy(h.Clock);

        // Act and assert
        Assert.False(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.InProgress, ReviewPolicy = ReviewPolicy.AfterGraded }));
        Assert.False(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.NeedsGrading, ReviewPolicy = ReviewPolicy.AfterGraded }));
        Assert.True(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.Graded, ReviewPolicy = ReviewPolicy.AfterGraded }));
        Assert.False(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.Graded, ReviewPolicy = ReviewPolicy.ScoreOnly }));
        Assert.False(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.Graded, ReviewPolicy = ReviewPolicy.AfterClose, QuizClosesUtc = LearningTestHarness.Now.AddDays(1) }));
        Assert.True(service.CanReview(new AssessmentAttempt { Status = AttemptStatus.Graded, ReviewPolicy = ReviewPolicy.AfterClose, QuizClosesUtc = LearningTestHarness.Now.AddDays(-1) }));
    }

    private static async Task<AssessmentAttempt> TakeAsync(AssessmentTestHarness h, string[] answers)
    {
        var started = await h.StartAsync("learner", "quiz");
        Assert.True(started.Succeeded, started.ErrorCode);

        for (var i = 0; i < answers.Length; i++)
        {
            var index = i;
            await h.RunAsync(service => service.SaveResponseAsync(started.Attempt.ItemId, "learner", index, new AttemptResponse { ChoiceIds = [answers[index]] }));
        }

        return (await h.RunAsync(service => service.SubmitAsync(started.Attempt.ItemId, "learner"))).Attempt;
    }
}
