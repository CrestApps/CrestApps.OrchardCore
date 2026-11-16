using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Review round 8, finding 7.1: a gradable item handed in last completes the course before it is graded; the grade that
/// arrives afterwards must still reach the course grade and outcome, and flag the enrollment when the outcome changes.
/// Gradable items of a kind graded by a person complete when graded.
/// </summary>
public sealed class GradeAfterCompletionTests
{
    [Fact]
    public async Task AGradeGivenAfterTheCourseCompleted_ReachesTheCourseGrade_AndFlagsAChangedOutcome()
    {
        // Arrange: a lesson and a gradable item that completes on hand-in, with a course pass mark of 50.
        var (harness, courses, _) = await LearningTestHarness.CreateAsync();
        await using var _ = harness;

        courses.AddCourse("c1", part => part.PassMark = 50);
        courses.AddItem("c1", "lesson");
        courses.AddItem("c1", "work", completionMode: CompletionModes.Submitted, configure: part => part.Gradable = true);

        var enrolled = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentService>().EnrollAsync(new EnrollmentRequest { UserId = "ana", CourseId = "c1", Source = "Test", SourceId = "ana", Actor = "admin" }));
        var enrollment = enrolled.Enrollment;

        await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordViewedAsync(enrollment, "lesson", 100));
        var handedIn = await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(enrollment, new CourseItemResult { ItemId = "work", Submitted = true }));

        // Act: the grader grades the work after the course completed.
        var graded = await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().RecordResultAsync(enrollment, new CourseItemResult { ItemId = "work", Score = 90, Graded = true, Submitted = true }));
        var after = await harness.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId));
        var progress = await harness.RunAsync(services => services.GetRequiredService<ILearnerProgressService>().GetAsync(enrollment.ItemId));

        // Assert
        Assert.Equal(EnrollmentStatus.Completed, handedIn.Enrollment.Status);
        Assert.Equal(false, handedIn.Enrollment.Passed);
        Assert.Null(graded.RefusedReason);
        Assert.Equal(90, progress.Find("work").Score);
        Assert.Equal(90, after.Grade);
        Assert.Equal(true, after.Passed);
        Assert.True(after.NeedsReview);
    }

    [Fact]
    public void AKindGradedByAPerson_AllowsOnlyGradedCompletion_WhenTheItemIsGradable()
    {
        // Arrange
        var kind = new CourseItemKindDescriptor("Coursework") { GradableCompletesWhenGraded = true };
        kind.CompletionModes.Add(CompletionModes.Graded);
        kind.CompletionModes.Add(CompletionModes.Submitted);

        // Act and assert
        Assert.True(kind.Allows(CompletionModes.Graded, gradable: true));
        Assert.False(kind.Allows(CompletionModes.Submitted, gradable: true));
        Assert.True(kind.Allows(CompletionModes.Submitted, gradable: false));
        Assert.False(kind.Allows(CompletionModes.Viewed, gradable: false));
    }

    [Fact]
    public void TheCourseworkKind_CompletesWhenGraded_ByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningCourseworkCore();

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<IOptions<CourseItemKindOptions>>().Value.Find("Coursework");

        // Assert
        Assert.True(kind.GradableCompletesWhenGraded);
        Assert.Equal(CompletionModes.Graded, kind.DefaultCompletionMode);
    }
}
