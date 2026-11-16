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

        var assisted = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 3, AIAssisted = true } }, null, "grader"));
        var changed = await h.RunAsync(service => service.GradeAsync(attempt.ItemId, new Dictionary<int, ManualScore> { [0] = new() { Points = 4 } }, null, "grader"));

        Assert.True(assisted.Attempt.Items[0].AIAssisted);
        Assert.False(changed.Attempt.Items[0].AIAssisted);
        Assert.Equal(AttemptStatus.Graded, changed.Attempt.Status);
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

        var assisted = await h.RunAsync(services => services.GetRequiredService<ISubmissionService>().GradeAsync(new GradeSubmissionRequest
        {
            SubmissionId = submitted.Submission.ItemId,
            Actor = "grader",
            Points = 70,
            AIAssisted = true,
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
