using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Work on an item after the course is completed (the end-of-course survey, an optional lesson, an acknowledgement) is
/// recorded on the item, so it reads "Done", while the enrollment stays exactly as it was completed: it never reopens,
/// and no completion or item observer fires again.
/// </summary>
public sealed class ProgressAfterCompletionTests
{
    [Fact]
    public async Task AnItemDoneAfterCompletion_IsRecorded_WithoutTouchingTheCompletedEnrollment()
    {
        // Arrange
        var clock = new LearningTestClock();
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync(clock.Use);
        await using var _ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson", completionMode: CompletionModes.MarkedDone);
        courses.AddItem("course1", "survey", completionMode: CompletionModes.Submitted, required: false);
        courses.AddItem("course1", "reading", required: false);
        courses.AddItem("course1", "policy", completionMode: CompletionModes.MarkedDone, required: false);
        var enrollment = await EnrollAsync(harness, "learner", "course1");
        var completing = await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "lesson"), enrollment.ItemId);
        var before = await FindAsync(harness, enrollment.ItemId);
        var eventsBefore = observer.Events.ToList();

        // Act
        clock.UtcNow = LearningTestHarness.Now.AddMinutes(5);
        var survey = await ProgressAsync(harness, (service, e) => service.RecordResultAsync(e, new CourseItemResult { ItemId = "survey", Submitted = true }), enrollment.ItemId);
        var reading = await ProgressAsync(harness, (service, e) => service.RecordAccessAsync(e, "reading", 0), enrollment.ItemId);
        var policy = await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "policy"), enrollment.ItemId);
        var progress = await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollment.ItemId), commit: false);
        var after = await FindAsync(harness, enrollment.ItemId);

        // Assert
        Assert.True(completing.CourseCompleted);
        Assert.Null(survey.RefusedReason);
        Assert.True(survey.ItemCompleted);
        Assert.False(survey.CourseCompleted);
        Assert.True(reading.ItemCompleted);
        Assert.True(policy.ItemCompleted);
        Assert.Equal(CourseItemState.Completed, progress.Find("survey").State);
        Assert.Equal(CourseItemState.Completed, progress.Find("reading").State);
        Assert.Equal(CourseItemState.Completed, progress.Find("policy").State);

        Assert.Equal(EnrollmentStatus.Completed, after.Status);
        Assert.Equal(before.CompletedUtc, after.CompletedUtc);
        Assert.Equal(before.ProgressPercent, after.ProgressPercent);
        Assert.Equal(before.Completion.Items.Count, after.Completion.Items.Count);
        Assert.Equal(eventsBefore, observer.Events);
    }

    [Fact]
    public async Task AnItemAlreadyDone_IsNotChangedAfterCompletion_AndAnEndedAccessRecordsNothing()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var _h = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "lesson", completionMode: CompletionModes.MarkedDone);
        courses.AddItem("course1", "extra", required: false);
        var enrollment = await EnrollAsync(harness, "learner", "course1");
        await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "lesson"), enrollment.ItemId);
        var completedAt = (await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollment.ItemId), commit: false)).Find("lesson").CompletedUtc;
        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CrestApps.OrchardCore.Learning.Core.Services.EnrollmentStore>();
            var found = await store.FindByIdAsync(enrollment.ItemId);

            found.AccessEndsUtc = LearningTestHarness.Now.AddDays(-1);
            await store.SaveAsync(found);

            return true;
        });

        // Act
        var again = await ProgressAsync(harness, (service, e) => service.MarkDoneAsync(e, "lesson"), enrollment.ItemId);
        var extra = await ProgressAsync(harness, (service, e) => service.RecordAccessAsync(e, "extra", 0), enrollment.ItemId);
        var progress = await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollment.ItemId), commit: false);

        // Assert
        Assert.False(again.ItemCompleted);
        Assert.Equal(completedAt, progress.Find("lesson").CompletedUtc);
        Assert.False(extra.ItemCompleted);
        Assert.Null(progress.Find("extra"));
    }

    private static Task<ProgressUpdate> ProgressAsync(CalendarTestHarness harness, Func<ILearnerProgressService, Enrollment, Task<ProgressUpdate>> action, string enrollmentId)
        => harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId);

            return await action(services.GetRequiredService<ILearnerProgressService>(), enrollment);
        });

    private static Task<Enrollment> FindAsync(CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(id), commit: false);

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness, string userId, string courseId)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = userId,
            CourseId = courseId,
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-" + userId,
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }
}
