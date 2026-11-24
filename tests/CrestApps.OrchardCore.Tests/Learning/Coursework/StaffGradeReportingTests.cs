using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Coursework.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Learning.Coursework;

/// <summary>
/// Review round 8, 7.2: a grade is a staff write. It reaches the gradebook after the learner's access window ended or
/// when a release rule locks the item again, is retried when the enrollment is busy, and fails with a reason (leaving the
/// work ungraded) when Learning still refuses it, instead of showing as graded and never counting.
/// </summary>
public sealed class StaffGradeReportingTests
{
    private const string Course = "course1";
    private const string Work = "essay1";

    [Fact]
    public async Task Grade_AfterTheLearnersAccessEnded_ReachesTheGradebook()
    {
        // Arrange: handed in on the last day, graded after the access window closed.
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await EndAccessAsync(h, ana.ItemId);

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);
        var records = await RecordsAsync(h, ana.ItemId);

        // Assert: one record, updated (a save after Learning's commit would have added a second one).
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(80m, progress?.Score);
        Assert.Equal(CourseItemState.Completed, progress?.State);
        Assert.Equal(SubmissionStatus.Graded, Assert.Single(records).Status);
    }

    [Fact]
    public async Task Return_AfterTheLearnersAccessEnded_TakesTheGradeOut_InOneRecord()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        await EndAccessAsync(h, ana.ItemId);

        // Act
        var returned = await h.ReturnAsync(submitted.Submission.ItemId, "grader");
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);
        var records = await RecordsAsync(h, ana.ItemId);

        // Assert
        Assert.True(returned.Succeeded, returned.Error);
        Assert.Null(progress?.Score);
        Assert.Equal(SubmissionStatus.Returned, Assert.Single(records).Status);
    }

    [Fact]
    public async Task Grade_OfAnItemAReleaseRuleLocksAgain_ReachesTheGradebook()
    {
        // Arrange: after the hand-in the item is put behind a lesson the learner has not completed.
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        h.Courses.Get(Work).Alter<CourseItemPart>(part => part.ReleaseRules =
        [
            new LearningRuleSettings { Name = LearningRuleNames.ItemCompleted, Values = { ["ItemId"] = "lesson1" } },
        ]);

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 70);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(70m, progress?.Score);
    }

    [Fact]
    public async Task Grade_OnASuspendedEnrollment_FailsWithAReason_AndTheWorkStaysUngraded()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(ana.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 80);
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.EnrollmentNotActive, graded.Error);
        Assert.Equal(SubmissionStatus.Submitted, stored.Status);
        Assert.Null(stored.Score);
        Assert.Null(stored.GradedUtc);
    }

    [Fact]
    public async Task Grade_WhenTheEnrollmentIsBusyOnce_IsRetried_AndReachesTheGradebook()
    {
        // Arrange: the enrollment lock is refused the first time the grade reaches it.
        var locks = new BusyLock(EnrollmentWriteGate.Key(string.Empty), refusals: 0);
        await using var h = await CreateAsync(locks);
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        locks.RefuseNext(1);

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 90);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.True(graded.Succeeded, graded.Error);
        Assert.Equal(1, locks.Refused);
        Assert.Equal(90m, progress?.Score);
    }

    [Fact]
    public async Task Grade_WhenTheEnrollmentStaysBusy_FailsBusy_AndTheWorkStaysUngraded()
    {
        // Arrange
        var locks = new BusyLock(EnrollmentWriteGate.Key(string.Empty), refusals: 0);
        await using var h = await CreateAsync(locks);
        await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        locks.RefuseNext(int.MaxValue);

        // Act
        var graded = await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 90);
        var stored = await h.FindAsync(submitted.Submission.ItemId);

        // Assert
        Assert.Equal(SubmissionErrors.Busy, graded.Error);
        Assert.Equal(SubmissionStatus.Submitted, stored.Status);
        Assert.Null(stored.Score);
    }

    [Fact]
    public async Task Return_OnASuspendedEnrollment_FailsWithAReason_AndKeepsTheGrade()
    {
        // Arrange
        await using var h = await CreateAsync();
        var ana = await h.EnrollAsync("ana", Course);
        var submitted = await h.SubmitAsync("ana", Work, "Answer");
        await h.GradeAsync(submitted.Submission.ItemId, "grader", points: 60);
        await h.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ChangeStatusAsync(ana.ItemId, EnrollmentStatus.Suspended, "admin", "Paused"));

        // Act
        var returned = await h.ReturnAsync(submitted.Submission.ItemId, "grader");
        var stored = await h.FindAsync(submitted.Submission.ItemId);
        var progress = await h.Inner.ProgressAsync(ana.ItemId, Work);

        // Assert
        Assert.Equal(SubmissionErrors.EnrollmentNotActive, returned.Error);
        Assert.Equal(SubmissionStatus.Graded, stored.Status);
        Assert.Equal(60m, stored.Score);
        Assert.Equal(60m, progress?.Score);
    }

    private static Task<IReadOnlyList<Submission>> RecordsAsync(CourseworkTestHarness h, string enrollmentId)
        => h.RunAsync(services => services.GetRequiredService<ISubmissionService>().ListForEnrollmentAsync(enrollmentId, Work));

    private static async Task EndAccessAsync(CourseworkTestHarness h, string enrollmentId)
        => await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<EnrollmentStore>();
            var enrollment = await store.FindByIdAsync(enrollmentId);

            enrollment.AccessEndsUtc = h.Clock.UtcNow.AddDays(-1);
            await store.SaveAsync(enrollment);

            return true;
        });

    private static async Task<CourseworkTestHarness> CreateAsync(IDistributedLock locks = null)
    {
        var h = await CourseworkTestHarness.CreateAsync(locks is null ? null : services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton(locks);
        });

        h.Courses.AddCourse(Course);

        // A lesson nobody opens keeps the course open: a completed enrollment is a record and takes no more work.
        h.Courses.AddItem(Course, "lesson1");
        h.AddCoursework(Course, Work, part =>
        {
            part.SubmissionType = CourseworkSubmissionType.Text;
            part.MaxPoints = 100;
        }, completionMode: CompletionModes.Graded);

        return h;
    }
}
