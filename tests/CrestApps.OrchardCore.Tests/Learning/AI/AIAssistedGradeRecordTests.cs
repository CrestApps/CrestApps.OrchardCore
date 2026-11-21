using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using CrestApps.OrchardCore.Tests.Learning.Coursework;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// A grade a grader saved after using scoring help says it was AI-assisted, on the essay answer and on the
/// submission, and the earlier grade kept in the history keeps saying so. A grade saved without help does not.
/// </summary>
public sealed class AIAssistedGradeRecordTests
{
    [Fact]
    public async Task EssayScore_SavedWithHelp_IsMarkedAIAssisted()
    {
        await using var h = await AssessmentTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Questions.AddEssay("essay", points: 4);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["essay"], completionMode: CompletionModes.Graded);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { Texts = ["Heat, fuel and oxygen."] }));
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        await h.RunAsync(service => service.RecordScoringHelpAsync(attempt.ItemId, 0, "grader"));
        var assisted = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 3 } }, null, "grader"));
        var changed = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 4 } }, null, "grader"));

        Assert.True(assisted.Attempt.Items[0].AIAssisted);
        Assert.False(changed.Attempt.Items[0].AIAssisted);
        Assert.Equal(AttemptStatus.Graded, changed.Attempt.Status);
    }

    [Fact]
    public async Task Round9M12_AFlagSentByTheBrowser_WithoutASuggestion_IsNotRecorded()
    {
        await using var h = await AssessmentTestHarness.CreateAsync();
        var attempt = await SubmittedEssayAsync(h);

        var claimed = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 3, AIAssisted = true } }, null, "grader"));

        Assert.False(claimed.Attempt.Items[0].AIAssisted);
    }

    [Fact]
    public async Task Round9M12_ASuggestionIssuedToTheGrader_MarksTheirNextScore_AndAnUnchangedScoreKeepsTheMark()
    {
        await using var h = await AssessmentTestHarness.CreateAsync();
        var attempt = await SubmittedEssayAsync(h);

        await h.RunAsync(service => service.RecordScoringHelpAsync(attempt.ItemId, 0, "grader"));
        var assisted = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 3 } }, null, "grader"));

        // The grader saves another question; the form sends the first essay again, unchanged, with no flag.
        var other = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 3 }, [1] = new() { Points = 2 } }, null, "grader"));
        var changed = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 1 } }, null, "grader"));

        Assert.True(assisted.Attempt.Items[0].AIAssisted);
        Assert.True(other.Attempt.Items[0].AIAssisted);
        Assert.False(other.Attempt.Items[1].AIAssisted);
        Assert.False(changed.Attempt.Items[0].AIAssisted);
    }

    private static async Task<AssessmentAttempt> SubmittedEssayAsync(AssessmentTestHarness h)
    {
        h.Courses.AddCourse("course1");
        h.Questions.AddEssay("essay", points: 4);
        h.Questions.AddEssay("essay2", points: 4);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["essay", "essay2"], completionMode: CompletionModes.Graded);
        await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { Texts = ["Heat, fuel and oxygen."] }));
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { Texts = ["Water."] }));
        await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        return attempt;
    }

    [Fact]
    public async Task Round10_1_ACourseworkFlagSentByTheBrowser_WithoutASuggestion_IsNotRecorded()
    {
        await using var h = await CourseworkTestHarness.CreateAsync();
        var submitted = await SubmittedCourseworkAsync(h);

        var claimed = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submitted.ItemId,
            Actor = "grader",
            Points = 70,
            AIAssisted = true,
        }));

        Assert.True(claimed.Succeeded, claimed.Error);
        Assert.False(claimed.Submission.AIAssisted);
        Assert.False((await h.FindAsync(submitted.ItemId)).AIAssisted);
    }

    [Fact]
    public async Task Round10_1_ASuggestionIssuedToTheGrader_MarksTheirNextGrade_AndTheSameScoreKeepsTheMark()
    {
        await using var h = await CourseworkTestHarness.CreateAsync();
        var submitted = await SubmittedCourseworkAsync(h);
        var service = (Func<IServiceProvider, ISubmissionService>)(services => services.GetRequiredService<ISubmissionService>());

        // A suggestion given to another grader marks nothing of this grader's.
        await h.RunAsync(services => service(services).RecordScoringHelpAsync(submitted.ItemId, "another-grader"));
        var unassisted = await h.RunAsync(services => service(services).GradeAsync(new GradeSubmissionRequest { SubmissionId = submitted.ItemId, Actor = "grader", Points = 60 }));

        await h.RunAsync(services => service(services).RecordScoringHelpAsync(submitted.ItemId, "grader"));
        var assisted = await h.RunAsync(services => service(services).GradeAsync(new GradeSubmissionRequest { SubmissionId = submitted.ItemId, Actor = "grader", Points = 70, Reason = "With help" }));
        var sameScore = await h.RunAsync(services => service(services).GradeAsync(new GradeSubmissionRequest { SubmissionId = submitted.ItemId, Actor = "grader", Points = 70, Reason = "Feedback only" }));
        var changed = await h.RunAsync(services => service(services).GradeAsync(new GradeSubmissionRequest { SubmissionId = submitted.ItemId, Actor = "grader", Points = 50, Reason = "Second look" }));

        Assert.False(unassisted.Submission.AIAssisted);
        Assert.True(assisted.Submission.AIAssisted);
        Assert.True(sameScore.Submission.AIAssisted);
        Assert.False(changed.Submission.AIAssisted);
        Assert.Null((await h.FindAsync(submitted.ItemId)).ScoringHelpGivenTo);
    }

    private static async Task<Submission> SubmittedCourseworkAsync(CourseworkTestHarness h)
    {
        h.Courses.AddCourse("course1");
        h.Courses.AddItem("course1", "lesson1");
        h.AddCoursework("course1", "essay1", part =>
        {
            part.SubmissionType = CourseworkSubmissionType.Text;
            part.MaxPoints = 100;
        });
        await h.EnrollAsync("ana", "course1");

        return (await h.SubmitAsync("ana", "essay1", "Answer")).Submission;
    }

    [Fact]
    public async Task SubmissionGrade_SavedWithHelp_IsMarkedAIAssisted_AndTheHistoryKeepsIt()
    {
        await using var h = await CourseworkTestHarness.CreateAsync();
        h.Courses.AddCourse("course1");
        h.Courses.AddItem("course1", "lesson1");
        h.AddCoursework("course1", "essay1", part =>
        {
            part.SubmissionType = CourseworkSubmissionType.Text;
            part.MaxPoints = 100;
        });
        await h.EnrollAsync("ana", "course1");
        var submitted = await h.SubmitAsync("ana", "essay1", "Answer");

        await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().RecordScoringHelpAsync(submitted.Submission.ItemId, "grader"));
        var assisted = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submitted.Submission.ItemId,
            Actor = "grader",
            Points = 70,
        }));
        var regraded = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submitted.Submission.ItemId,
            Actor = "grader",
            Points = 80,
            Reason = "Second look",
        }));
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        Assert.True(assisted.Succeeded, assisted.Error);
        Assert.True(assisted.Submission.AIAssisted);
        Assert.True(regraded.Succeeded, regraded.Error);
        Assert.False(stored.AIAssisted);
        Assert.True(Assert.Single(stored.History).AIAssisted);
    }
}
