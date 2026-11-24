using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// A completed enrollment is a record: when a result it rests on is voided or lowered afterwards, the enrollment stays
/// Completed and is flagged for a person to review, instead of being silently un-completed or left as it was.
/// </summary>
public sealed class EnrollmentReviewTests
{
    [Fact]
    public async Task AResultUndoneAfterCompletion_FlagsTheEnrollmentForReview_WithoutUncompletingIt()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "exam", completionMode: CompletionModes.Passed);
        var enrollment = await EnrollAsync(harness);
        var passed = await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", Score = 90, Passed = true, Submitted = true, Graded = true, Attempts = 1 });

        // Act: the exam attempt is voided for cheating.
        var voided = await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", ResetScore = true, Attempts = 0 });

        // Assert
        Assert.True(passed.CourseCompleted);
        var stored = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId), commit: false);
        Assert.Equal(EnrollmentStatus.Completed, stored.Status);
        Assert.True(stored.NeedsReview);
        Assert.Equal("exam", stored.Review.ItemId);
        Assert.Equal(LearningReviewReasons.ResultChangedAfterCompletion, stored.Review.Reason);
        Assert.Equal(90m, stored.Review.PreviousScore);
        Assert.Contains("ReviewNeeded", observer.Events);
        Assert.NotNull(voided.Enrollment);

        var listed = await harness.RunAsync(services => services.GetRequiredService<EnrollmentStore>().PageAsync(new EnrollmentQuery { NeedsReview = true }, 1, 10), commit: false);
        Assert.Equal(enrollment.ItemId, Assert.Single(listed.Items).ItemId);
    }

    [Fact]
    public async Task AHigherRegradeAfterCompletion_FlagsNothing()
    {
        // Arrange
        var (harness, courses, observer) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "exam", completionMode: CompletionModes.Passed);
        var enrollment = await EnrollAsync(harness);
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", Score = 70, Passed = true, Submitted = true, Graded = true, Attempts = 1 });

        // Act
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", Score = 85, Passed = true, Submitted = true, Graded = true, Attempts = 1 });

        // Assert
        var stored = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId), commit: false);
        Assert.False(stored.NeedsReview);
        Assert.DoesNotContain("ReviewNeeded", observer.Events);
    }

    [Fact]
    public async Task ClearingAReview_RecordsWhoAndWhy_AndTakesTheEnrollmentOffTheList()
    {
        // Arrange
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var __ = harness;
        courses.AddCourse("course1");
        courses.AddItem("course1", "exam", completionMode: CompletionModes.Passed);
        var enrollment = await EnrollAsync(harness);
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", Score = 90, Passed = true, Submitted = true, Graded = true, Attempts = 1 });
        await Result(harness, enrollment.ItemId, new CourseItemResult { ItemId = "exam", ResetScore = true });

        // Act
        var cleared = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ClearReviewAsync(enrollment.ItemId, "manager", "Retake booked; certificate kept"));
        var refused = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().ClearReviewAsync(enrollment.ItemId, "manager", " "));

        // Assert
        Assert.True(cleared.Succeeded, cleared.ErrorCode);
        Assert.Equal(EnrollmentErrors.ReasonRequired, refused.ErrorCode);
        var stored = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId), commit: false);
        Assert.False(stored.NeedsReview);
        Assert.Equal("manager", stored.Review.ClearedBy);
        Assert.Equal("Retake booked; certificate kept", stored.Review.ClearNote);
        var listed = await harness.RunAsync(services => services.GetRequiredService<EnrollmentStore>().PageAsync(new EnrollmentQuery { NeedsReview = true }, 1, 10), commit: false);
        Assert.Empty(listed.Items);
    }

    private static async Task<Enrollment> EnrollAsync(CalendarTestHarness harness)
    {
        var result = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest
        {
            UserId = "learner",
            CourseId = "course1",
            Source = LearningConstants.EnrollmentSources.Manual,
            SourceId = "m-learner",
        }));

        Assert.True(result.Succeeded, result.ErrorCode);

        return result.Enrollment;
    }

    private static Task<ProgressUpdate> Result(CalendarTestHarness harness, string enrollmentId, CourseItemResult result)
        => harness.RunAsync(async services =>
        {
            var enrollment = await services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollmentId);

            return await services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(enrollment, result);
        });
}
