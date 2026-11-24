using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Review round 8, 7.2: grading an attempt by hand is a staff write. The score reaches the gradebook after the learner's
/// access window ended, is retried when the enrollment is busy, and the grading fails with a reason (leaving the attempt
/// as it was) when Learning still refuses it.
/// </summary>
public sealed class StaffAttemptGradeReportingTests
{
    [Fact]
    public async Task Grade_AfterTheLearnersAccessEnded_ReachesTheGradebook()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var (enrollment, attempt) = await SubmitEssayAsync(h);
        await EndAccessAsync(h, enrollment.ItemId);

        // Act
        var graded = await GradeAsync(h, attempt.ItemId);
        var progress = await h.ProgressAsync(enrollment.ItemId, "quiz");
        var records = await RecordsAsync(h);

        // Assert: one record, updated (a save after Learning's commit would have added a second one).
        Assert.True(graded.Succeeded, graded.ErrorCode);
        Assert.Equal(80m, progress?.Score);
        Assert.Equal(CourseItemState.Completed, progress?.State);
        Assert.Equal(AttemptStatus.Graded, Assert.Single(records).Status);
    }

    [Fact]
    public async Task RegradeAndVoid_AfterTheLearnersAccessEnded_ReachTheGradebook_InOneRecord()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var (enrollment, attempt) = await SubmitEssayAsync(h);
        await GradeAsync(h, attempt.ItemId);
        await EndAccessAsync(h, enrollment.ItemId);

        // Act
        var regraded = await h.RunAsync(service => service.RegradeAsync(attempt.ItemId, "grader", "Key fixed"));
        var voided = await h.RunAsync(service => service.VoidAsync(attempt.ItemId, "grader", "Cheating"));
        var progress = await h.ProgressAsync(enrollment.ItemId, "quiz");
        var records = await RecordsAsync(h);

        // Assert
        Assert.True(regraded.Succeeded, regraded.ErrorCode);
        Assert.True(voided.Succeeded, voided.ErrorCode);
        Assert.Null(progress?.Score);
        var record = Assert.Single(records);
        Assert.Equal(AttemptStatus.Voided, record.Status);
        Assert.Equal(["Submitted", "Graded", "Regraded", "Voided"], record.ScoreHistory.Select(change => change.Kind));
    }

    [Fact]
    public async Task Grade_OnASuspendedEnrollment_FailsWithAReason_AndTheAttemptStaysUngraded()
    {
        // Arrange
        await using var h = await AssessmentTestHarness.CreateAsync();
        var (enrollment, attempt) = await SubmitEssayAsync(h);
        await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(enrollment.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));

        // Act
        var graded = await GradeAsync(h, attempt.ItemId);
        var stored = await h.RunAsync(services => services.GetRequiredService<AssessmentAttemptStore>().FindByIdAsync(attempt.ItemId).AsTask());

        // Assert
        Assert.Equal(AttemptErrors.EnrollmentNotActive, graded.ErrorCode);
        Assert.Equal(AttemptStatus.NeedsGrading, stored.Status);
        Assert.Null(stored.GradedBy);
        Assert.Null(stored.Items[1].ManualScore);
    }

    [Fact]
    public async Task Grade_WhenTheEnrollmentIsBusyOnce_IsRetried_AndReachesTheGradebook()
    {
        // Arrange
        var locks = new BusyLock(EnrollmentWriteGate.Key(string.Empty));
        await using var h = await AssessmentTestHarness.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock>(locks);
        });
        var (enrollment, attempt) = await SubmitEssayAsync(h);
        locks.RefuseNext(1);

        // Act
        var graded = await GradeAsync(h, attempt.ItemId);
        var progress = await h.ProgressAsync(enrollment.ItemId, "quiz");

        // Assert
        Assert.True(graded.Succeeded, graded.ErrorCode);
        Assert.Equal(1, locks.Refused);
        Assert.Equal(80m, progress?.Score);
    }

    private static Task<AttemptResult> GradeAsync(AssessmentTestHarness h, string attemptId)
        => h.RunAsync(service => service.GradeAsync(attemptId, new Dictionary<int, ManualScore> { [1] = new() { Points = 3 } }, "Good.", "grader"));

    private static async Task<(Enrollment Enrollment, AssessmentAttempt Attempt)> SubmitEssayAsync(AssessmentTestHarness h)
    {
        h.Courses.AddCourse("course1");

        // A lesson nobody opens keeps the course open.
        h.Courses.AddItem("course1", "lesson1");
        h.Questions.AddChoice("q1", "a");
        h.Questions.AddEssay("essay", points: 4);
        h.AddQuiz("course1", "quiz", quiz => quiz.QuestionIds = ["q1", "essay"], completionMode: CompletionModes.Graded);
        var enrollment = await h.EnrollAsync("learner", "course1");
        var attempt = (await h.StartAsync("learner", "quiz")).Attempt;
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 0, new AttemptResponse { ChoiceIds = ["a"] }));
        await h.RunAsync(service => service.SaveResponseAsync(attempt.ItemId, "learner", 1, new AttemptResponse { Texts = ["Heat, fuel and oxygen."] }));
        var submitted = await h.RunAsync(service => service.SubmitAsync(attempt.ItemId, "learner"));

        Assert.Equal(AttemptStatus.NeedsGrading, submitted.Attempt.Status);

        return (enrollment, submitted.Attempt);
    }

    private static Task<IReadOnlyList<AssessmentAttempt>> RecordsAsync(AssessmentTestHarness h)
        => h.RunAsync(services => services.GetRequiredService<AssessmentAttemptStore>().ListAsync("learner", "quiz"));

    private static async Task EndAccessAsync(AssessmentTestHarness h, string enrollmentId)
        => await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(enrollmentId);

            enrollment.AccessEndsUtc = h.Clock.UtcNow.AddDays(-1);
            await store.SaveAsync(enrollment);

            return true;
        });
}
